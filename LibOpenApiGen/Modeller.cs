using System.Text;
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
                if (property.Value.Type == "array")
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

        private static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties)
        {
            string type;
            string name;
            foreach (var property in properties)
            {
                if (property.Value.Ref != null)
                {
                    var Ref = property.Value.Ref.Replace("#/components/schemas/", "");
                    type = $"{Ref}";
                }
                else
                {
                    type = $"{property.Value.Type.ToLower()}";
                }
                if (property.Value.Type == "array")
                {
                    useList = true;
                    if (property.Value.Items == null) continue;
                    if (property.Value.Items.Type != null)
                    {
                        type = $"List<{property.Value.Items.Type}>";
                        if (property.Value.Items.Ref != null)
                        {
                            var Ref = property.Value.Items.Ref.Replace("#/components/schemas/", "");
                            type = $"List<{Ref}>";
                        }
                        if (property.Value.Items != null && property.Value.Items.Items != null && property.Value.Items.Items.Type != null)
                        {
                            type = $"List<{property.Value.Items.Items.Type}>";
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
                if (type == "boolean")
                {
                    type = "bool";
                }
                if (property.Value.Nullable.GetValueOrDefault(false) || property.Value.Optional.GetValueOrDefault(false))
                {
                    type += "?";
                }
                name = ConvertToPascalCase(property.Key);
                sb.Append($"\t\tpublic {type} {name} {{ get; set; }}\n");
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
            //else if (component.OneOf != null)
            //{
            //    sb.Append("\t\tpublic JsonNode json {get;set;}\n");
            //    foreach (var property in component.OneOf)
            //    {
            //        sb.Append($"\t\tpublic {property.Ref}? {key}{property.Ref} {{get => JsonSerializer.Deserialize<{property.Ref}>(json);}}\n");
            //    }
            //    sb.Append($"\t\tpublic string {key}TypeName {{\n");
            //    sb.Append("\t\t\tget {\n");
            //    sb.Append($"\t\t\t\treturn {key + component.OneOf[0].Ref} != null ? \"{key}\" : {key + component.OneOf[1].Ref} != null ? \"{key}\" : \"InvalidType\";\n");
            //    sb.Append("\t\t\t}\n");
            //    sb.Append("\t\t}\n");
            //}
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