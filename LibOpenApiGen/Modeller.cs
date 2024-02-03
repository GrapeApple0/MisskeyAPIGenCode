using System.Text;
using System.Text.Json.Nodes;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
    public static class Modeller
    {
        private static bool useList = false;

        private static Dictionary<string, Property> ReturnAllPropertiesDictionary(ComponentsMethod component, Dictionary<string, ComponentsMethod> root)
        {
            var properties = new Dictionary<string, Property>();
            var components = component.OneOf != null ? component.OneOf : component.AllOf;
            foreach (var componentOf in components!)
            {
                if (componentOf.Ref == null) continue;
                var Ref = componentOf.Ref.Replace("#/components/schemas/", "");
                if (Ref == null) continue;
                if (root[Ref].Properties != null)
                {
                    foreach (var property in root[Ref].Properties!)
                    {
                        properties[property.Key] = property.Value;
                    }
                }
                else
                {
                    var res = ReturnAllPropertiesDictionary(root[Ref], root);
                    properties = properties.Concat(res.Where(pair =>
                        !properties.ContainsKey(pair.Key))
                      ).ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value
                      );
                }
            }
            return properties;
        }

        private static void GenerateToStringCode(StringBuilder sb, Dictionary<string, Property> properties, string key)
        {
            sb.Append("\t\tpublic override string ToString()\n");
            sb.Append("\t\t{\n");
            sb.Append("\t\t\tvar sb = new StringBuilder();\n");
            sb.Append($"\t\t\tsb.Append(\"class {key}: {{\\n\");\n");
            foreach (var property in properties)
            {
                var type = "";
                if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                {
                    type = s;
                }
                else if (property.Value.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                {
                    type = ja[0].ToString();
                }
                if (type == "array")
                {
                    sb.Append($"\t\t\tsb.Append(\"  {property.Key}: {{\\n\");\n");
                    if (property.Value.Items != null && property.Value.Items.Ref != null)
                    {
                        sb.Append($"\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0)\n");
                        sb.Append("\t\t\t{\n");
                        sb.Append("\t\t\t\tvar sb2 = new StringBuilder();\n");
                        sb.Append("\t\t\t\tsb2.Append(\"    \");\n");
                        sb.Append($"\t\t\t\tthis.{ConvertToPascalCase(property.Key)}.ForEach(item =>\n");
                        sb.Append("\t\t\t\t{\n");
                        sb.Append("\t\t\t\t\tsb2.Append(item).Append(\",\");\n");
                        sb.Append($"\t\t\t\t\tif (item != this.{ConvertToPascalCase(property.Key)}.Last()) sb2.Append(\"\\n\");\n");
                        sb.Append("\t\t\t\t});\n");
                        sb.Append("\t\t\t\tsb2.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append("\t\t\t\tsb2.Append(\"\\n\");\n");
                        sb.Append("\t\t\t\tsb.Append(sb2);\n");
                        sb.Append("\t\t\t}\n");
                    }
                    else
                    {
                        sb.Append($"\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0) this.{ConvertToPascalCase(property.Key)}.ForEach(item => sb.Append(\"    \").Append(item).Append(\",\\n\"));\n");
                    }
                    sb.Append("\t\t\tsb.Append(\"  }\\n\");\n");
                }
                else
                {
                    if (property.Value.Ref != null)
                    {
                        // sb.Append($"\t\t\tsb.Append(\"  {property.Key}: {{\\n\").Append(\"    \").Append(this.{Regex.Replace(property.Key, @"\b\p{Ll}", match => match.Value.ToUpper())}).Replace(\"\\n\", \"\\n    \").Append(\"\\n\").Append(\"  }}");
                        sb.Append($"\t\t\tvar sb{property.Key} = new StringBuilder();\n");
                        sb.Append($"\t\t\tsb{property.Key}.Append(\"  {property.Key}: {{\\n\");\n");
                        sb.Append($"\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null)\n");
                        sb.Append("\t\t\t{\n");
                        sb.Append($"\t\t\t\tsb{property.Key}.Append(this.{ConvertToPascalCase(property.Key)});\n");
                        sb.Append($"\t\t\t\tsb{property.Key}.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append($"\t\t\t\tsb{property.Key}.Append(\"\\n\");\n");
                        sb.Append("\t\t\t}\n");
                        sb.Append($"\t\t\tsb{property.Key}.Append(\"  }}\\n\");\n");
                        sb.Append($"\t\t\tsb.Append(sb{property.Key});\n");
                    }
                    else
                    {
                        sb.Append($"\t\t\tsb.Append($\"  {property.Key}: {{this.{ConvertToPascalCase(property.Key)}}}\\n\");\n");
                    }
                }
            }
            sb.Append("\t\t\tsb.Append(\"}\");\n");
            sb.Append("\t\t\treturn sb.ToString();\n");
            sb.Append("\t\t}\n");
        }

        private static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties, int indent = 2, bool addTypes = true)
        {
            string type;
            string name;
            foreach (var property in properties)
            {
                var indentStr = "";
                for (int i = 0; i < indent; i++) indentStr += "\t";
                bool nullable = false;
                name = ConvertToPascalCase(property.Key);
                if (property.Value.Type == null) type = "object";
                if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                {
                    type = s;
                }
                else if (property.Value.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                {
                    type = ja[0].ToString();
                }
                else
                {
                    var st = property.Value.Type.ToString();
                    type = property.Value.Type != null && st != null ? st : "";
                }
                if (property.Value.Ref != null)
                {
                    var Ref = property.Value.Ref.Replace("#/components/schemas/", "");
                    type = $"{Ref}";
                }
                else
                {
                    type = $"{type.ToLower()}";
                }
                if (type == "array")
                {
                    useList = true;
                    if (property.Value.Items == null) continue;
                    if (property.Value.Items.Type != null)
                    {
                        string itemsType = "";
                        bool nullableItems = false;
                        if (property.Value.Items.Type is JsonValue jv2 && jv2.AsValue().TryGetValue<string>(out var s2))
                        {
                            itemsType = s2;
                        }
                        else if (property.Value.Items.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                        {
                            itemsType = ja[0].ToString();
                            nullableItems = true;
                        }
                        type = $"List<{itemsType}>";
                        if (property.Value.Items.Ref != null)
                        {
                            var Ref = property.Value.Items.Ref.Replace("#/components/schemas/", "");
                            type = $"List<{Ref}>";
                        }
                        else if (itemsType == "object" && property.Value.Items != null)
                        {
                            if (addTypes) sb.Append($"{indentStr}public class {name}ItemType {{\n");
                            else sb.Append($"{indentStr}public class {name}Type {{\n");
                            GeneratePropertiesCode(sb, new Dictionary<string, Property>() { { name, property.Value.Items } }, indent + 1, false);
                            sb.Append($"{indentStr}}}\n");
                            type = $"List<{name}ItemType>";
                        }
                        if (nullableItems)
                        {
                            type += "?";
                        }
                        if (property.Value.Items != null && property.Value.Items.Items != null && property.Value.Items.Items.Type != null)
                        {
                            string itemsItemType = "";
                            bool nullableItemsItems = false;
                            if (property.Value.Items.Type is JsonValue jv3 && jv3.AsValue().TryGetValue<string>(out var s3))
                            {
                                itemsItemType = s3;
                            }
                            else if (property.Value.Items.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                            {
                                itemsItemType = ja[0].ToString();
                                nullableItemsItems = true;
                            }
                            type = $"List<List<{itemsItemType}>>";
                            if (property.Value.Items.Items.Ref != null)
                            {
                                var Ref = property.Value.Items.Items.Ref.Replace("#/components/schemas/", "");
                                type = $"List<List<{Ref}>>";
                            }
                            else if (property.Value.Items.Items != null)
                            {
                                if (addTypes) sb.Append($"{indentStr}public class {name}ItemsItemType {{\n");
                                else sb.Append($"{indentStr}public class {name}ItemType {{\n");
                                GeneratePropertiesCode(sb, new Dictionary<string, Property>() { { name, property.Value.Items.Items } }, indent + 1, false);
                                sb.Append($"{indentStr}}}\n");
                                type = $"List<List<{name}ItemsItemType>>";
                            }
                            if (nullableItemsItems)
                            {
                                type += "?";
                            }
                        }
                    }
                }
                if (property.Value.Format == "date-time")
                {
                    type = "DateTime";
                }
                if (type == "number")
                {
                    type = "decimal";
                }
                if (type == "integer")
                {
                    type = "int";
                }
                if (type == "boolean")
                {
                    type = "bool";
                }
                if (nullable)
                {
                    type += "?";
                }
                sb.Append($"{indentStr}public {type} {name} {{ get; set; }}\n");
            }
        }

        public static string GenerateModelCode(Dictionary<string, ComponentsMethod> root, string key, string ns)
        {
            var component = root[key];
            string code;
            var sb = new StringBuilder();
            if (component.OneOf != null) sb.Append("using System.Text.Json;\n");
            sb.Append("using System.Text;\n");
            sb.Append($"namespace {ns} {{\n");
            sb.Append($"\tpublic class {key} {{\n");
            if (component.Properties != null)
            {
                GeneratePropertiesCode(sb, component.Properties);
                GenerateToStringCode(sb, component.Properties, key);
            }
            else if (component.OneOf != null || component.AllOf != null)
            {
                var properties = ReturnAllPropertiesDictionary(component, root);
                GeneratePropertiesCode(sb, properties);
                GenerateToStringCode(sb, properties, key);
            }
            sb.Append("\t}\n");
            sb.Append("}\n");
            if (useList) sb.Insert(0, "using System.Collections.Generic;\n");
            code = sb.ToString();
            return code;
        }
    }
}