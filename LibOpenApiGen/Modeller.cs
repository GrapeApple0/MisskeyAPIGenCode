using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
    public static class Modeller
    {
        private static bool useList = false;

        public static Dictionary<string, Property> ReturnAllPropertiesDictionary(ComponentsMethod component, Dictionary<string, ComponentsMethod> root)
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

        private static void GenerateToStringCode(StringBuilder sb, Dictionary<string, Property> properties, string key, int indent = 2)
        {
            var indentStr = "";
            for (int i = 0; i < indent; i++) indentStr += "\t";
            sb.Append($"{indentStr}public override string ToString()\n");
            sb.Append($"{indentStr}{{\n");
            sb.Append($"{indentStr}\tvar sb = new StringBuilder();\n");
            sb.Append($"{indentStr}\tsb.Append(\"class {key}: {{\\n\");\n");
            foreach (var property in properties)
            {
                var type = "";
                if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                {
                    type = s;
                }
                else if (property.Value.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                {
                    type = ja[0]?.ToString() ?? "JsonNode";
                }
                if (type == "array")
                {
                    sb.Append($"{indentStr}\tsb.Append(\"  {property.Key}: {{\\n\");\n");
                    if (property.Value.Items != null && property.Value.Items.Ref != null)
                    {
                        sb.Append($"{indentStr}\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0)\n");
                        sb.Append($"{indentStr}\t{{\n");
                        sb.Append($"{indentStr}{indentStr}var sb2 = new StringBuilder();\n");
                        sb.Append($"{indentStr}{indentStr}sb2.Append(\"    \");\n");
                        sb.Append($"{indentStr}\t\tthis.{ConvertToPascalCase(property.Key)}.ForEach(item =>\n");
                        sb.Append($"{indentStr}\t\t{{\n");
                        sb.Append($"{indentStr}\t\t\tsb2.Append(item).Append(\",\");\n");
                        sb.Append($"{indentStr}\t\t\tif (item != this.{ConvertToPascalCase(property.Key)}.Last()) sb2.Append(\"\\n\");\n");
                        sb.Append($"{indentStr}\t\t}});\n");
                        sb.Append($"{indentStr}\t\tsb2.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append($"{indentStr}\t\tsb2.Append(\"\\n\");\n");
                        sb.Append($"{indentStr}\t\tsb.Append(sb2);\n");
                        sb.Append($"{indentStr}\t}}\n");
                    }
                    else
                    {
                        sb.Append($"\t{indentStr}if (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0) this.{ConvertToPascalCase(property.Key)}.ForEach(item => sb.Append(\"    \").Append(item).Append(\",\\n\"));\n");
                    }
                    sb.Append($"{indentStr}\tsb.Append(\"  }}\\n\");\n");
                }
                else
                {
                    if (property.Value.Ref != null)
                    {
                        // sb.Append($"\t\t\tsb.Append(\"  {property.Key}: {{\\n\").Append(\"    \").Append(this.{Regex.Replace(property.Key, @"\b\p{Ll}", match => match.Value.ToUpper())}).Replace(\"\\n\", \"\\n    \").Append(\"\\n\").Append(\"  }}");
                        sb.Append($"{indentStr}\tvar sb{property.Key} = new StringBuilder();\n");
                        sb.Append($"{indentStr}\tsb{property.Key}.Append(\"  {property.Key}: {{\\n\");\n");
                        sb.Append($"{indentStr}\tif (this.{ConvertToPascalCase(property.Key)} != null)\n");
                        sb.Append($"{indentStr}\t{{\n");
                        sb.Append($"{indentStr}\t\tsb{property.Key}.Append(this.{ConvertToPascalCase(property.Key)});\n");
                        sb.Append($"{indentStr}\t\tsb{property.Key}.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append($"{indentStr}\t\tsb{property.Key}.Append(\"\\n\");\n");
                        sb.Append($"{indentStr}\t}}\n");
                        sb.Append($"{indentStr}\tsb{property.Key}.Append(\"  }}\\n\");\n");
                        sb.Append($"{indentStr}\tsb.Append(sb{property.Key});\n");
                    }
                    else
                    {
                        sb.Append($"{indentStr}\tsb.Append($\"  {property.Key}: {{this.{ConvertToPascalCase(property.Key)}}}\\n\");\n");
                    }
                }
            }
            sb.Append($"{indentStr}\tsb.Append(\"}}\");\n");
            sb.Append($"{indentStr}\treturn sb.ToString();\n");
            sb.Append($"{indentStr}}}\n");
        }

        public static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties, string key, int indent = 2, bool addTypes = true)
        {
            string type;
            string name;
            foreach (var property in properties)
            {
                var indentStr = "";
                for (int i = 0; i < indent; i++) indentStr += "\t";
                bool nullable = false;
                name = ConvertToPascalCase(property.Key);
                if (property.Value.Type == null) type = "JsonNode";
                if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                {
                    type = s;
                }
                else if (property.Value.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
                {
                    type = ja[0]?.ToString() ?? "JsonNode";
                }
                else
                {
                    var st = property.Value.Type?.ToString();
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
                    if (property.Value.Items.Ref != null)
                    {
                        var Ref = property.Value.Items.Ref.Replace("#/components/schemas/", "");
                        type = $"List<{Ref}>";
                    }
                    if (property.Value.Items != null)
                    {
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
                                itemsType = ja[0]?.ToString() ?? "JsonNode";
                                nullableItems = true;
                            }
                            type = $"List<{itemsType}>";
                            if (itemsType == "JsonNode" && property.Value.Items != null)
                            {
                                if (addTypes) sb.Append($"{indentStr}public class {name}ItemType {{\n");
                                else sb.Append($"{indentStr}public class {name}Type {{\n");
                                GeneratePropertiesCode(sb, new Dictionary<string, Property>() { { name, property.Value.Items } }, key, indent + 1, false);
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
                                else if (property.Value.Items.Items != null && property.Value.Items.Items.Type != null)
                                {
                                    type = $"List<List<{property.Value.Items.Items.Type}>>";
                                }
                                if (nullableItemsItems)
                                {
                                    type += "?";
                                }
                            }
                        }

                    }
                    else
                    {
                        type = "List<JsonNode>";
                    }
                }
                if (property.Value.Format == "date-time") type = "DateTime";
                if (type == "number") type = "decimal";
                if (type == "integer") type = "int";
                if (type == "boolean") type = "bool";
                if (type == "object")
                {
                    type = "JsonNode";
                    if (property.Value.Properties != null)
                    {
                        type = $"{key}{name}Object";
                        sb.Append($"{indentStr}public class {key}{name}Object {{\n");
                        GeneratePropertiesCode(sb, property.Value.Properties, key, indent + 1);
                        sb.Append($"{indentStr}}}\n");
                    }
                    else if (property.Value.AllOf != null)
                    {
                        if (property.Value.AllOf.Count == 1)
                        {
                            type = property.Value.AllOf[0]["ref"].Replace("#/components/schemas/", "");
                        }
                    }
                }
                if (nullable)
                {
                    type += "?";
                }
                if (Regex.IsMatch(name, "^[0-9]")) name = "_" + name;
                sb.Append($"{indentStr}public {type} {name} {{ get; set; }}\n");
            }
        }

        public static string GenerateModelCode(Dictionary<string, ComponentsMethod> root, string key, string ns)
        {
            var component = root[key];
            string code;
            var sb = new StringBuilder();
            sb.Append("using System.Text.Json;\n");
            sb.Append("using System.Text.Json.Nodes;\n");
            sb.Append("using System.Text;\n");
            sb.Append($"namespace {ns} {{\n");
            if (Regex.IsMatch(key, "^[0-9]")) key = "_" + key;
            sb.Append($"\tpublic class {key} {{\n");
            if (component.Properties != null)
            {
                GeneratePropertiesCode(sb, component.Properties, key);
                GenerateToStringCode(sb, component.Properties, key);
            }
            else if (component.OneOf != null || component.AllOf != null)
            {
                var properties = ReturnAllPropertiesDictionary(component, root);
                GeneratePropertiesCode(sb, properties, key);
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