using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static LibOpenApiGen.ApiDocument;
using static LibOpenApiGen.ApiDocument.PathsMethod;

namespace LibOpenApiGen
{
    public static class Controller
    {
        public static Dictionary<string, Property> ReturnAllPropertiesDictionary(Schema component, Dictionary<string, ComponentsMethod> root)
        {
            var properties = new Dictionary<string, Property>();
            var components = component.OneOf != null ? component.OneOf : component.AnyOf;
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
                    var res = Modeller.ReturnAllPropertiesDictionary(root[Ref], root);
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

        private static string GetPropertyType(Property property)
        {
            string rawType = "";
            bool nullable = false;
            if (property.Type == null)
            {
                rawType = "JsonNode";
            }
            else if (property.Type is JsonValue jv)
            {
                jv.AsValue().TryGetValue<string>(out var s);
                rawType = s;
            }
            else if (property.Type is JsonArray ja)
            {
                if (ja != null && ja.Count > 0 && ja[0] != null) rawType = ja[0].ToString();
                nullable = true;
            }
            else
            {
                var st = property.Type.ToString();
                rawType = property.Type != null && st != null ? st : "";
            }
            string type;
            if (property.Ref != null)
            {
                var Ref = property.Ref.Replace("#/components/schemas/", "");
                type = $"Model.{Ref}";
            }
            else
            {
                type = $"{rawType.ToLower()}";
            }
            if (rawType == "array")
            {
                if (property.Items == null) type = "List<JsonNode>";
                else if (property.Items.Type != null)
                {
                    type = $"List<{property.Items.Type}>";
                    if (property.Items.Ref != null)
                    {
                        var Ref = property.Items.Ref.Replace("#/components/schemas/", "");
                        type = $"List<Model.{Ref}>";
                    }
                    if (property.Items != null && property.Items.Items != null && property.Items.Items.Type != null)
                    {
                        type = $"List<List<{property.Items.Items.Type}>>";
                    }
                }
            }
            if (property.Format == "date-time") type = "DateTime";
            if (type == "number") type = "decimal";
            if (type == "boolean") type = "bool";
            if (type == "integer") type = "int";
            if (type == "object") type = "JsonNode";
            if (nullable) type += "?";
            return type;
        }

        private static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties)
        {
            string type;
            string name;
            foreach (var property in properties)
            {
                type = GetPropertyType(property.Value);
                name = ConvertToPascalCase(property.Key);
                sb.Append($"\t\t\tpublic {type} {name} {{ get; set; }}\n");
            }
        }

        private static void GenerateToStringCode(StringBuilder sb, Dictionary<string, Property> properties)
        {
            sb.Append("\t\t\tpublic override string ToString()\n");
            sb.Append("\t\t\t{\n");
            sb.Append("\t\t\t\tvar sb = new StringBuilder();\n");
            sb.Append("\t\t\t\tsb.Append(\"{\\n\");\n");
            foreach (var property in properties)
            {
                string rawType = "";
                if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                {
                    rawType = s;
                }
                else if (property.Value.Type is JsonArray ja)
                {
                    if (ja != null && ja.Count > 0 && ja[0] != null) rawType = ja[0].ToString();
                }
                if (rawType == "array")
                {
                    sb.Append($"\t\t\t\tsb.Append(\"  {property.Key}: {{\\n\");\n");
                    if (property.Value.Items != null && property.Value.Items.Ref != null)
                    {
                        sb.Append($"\t\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0)\n");
                        sb.Append("\t\t\t\t{\n");
                        sb.Append("\t\t\t\t\tvar sb2 = new StringBuilder();\n");
                        sb.Append("\t\t\t\t\tsb2.Append(\"    \");\n");
                        sb.Append($"\t\t\t\t\tthis.{ConvertToPascalCase(property.Key)}.ForEach(item =>\n");
                        sb.Append("\t\t\t\t\t{\n");
                        sb.Append("\t\t\t\t\t\tsb2.Append(item).Append(\",\");\n");
                        sb.Append($"\t\t\t\t\t\tif (item != this.{ConvertToPascalCase(property.Key)}.Last()) sb2.Append(\"\\n\");\n");
                        sb.Append("\t\t\t\t\t});\n");
                        sb.Append("\t\t\t\t\tsb2.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append("\t\t\t\t\tsb2.Append(\"\\n\");\n");
                        sb.Append("\t\t\t\t\tsb.Append(sb2);\n");
                        sb.Append("\t\t\t\t}\n");
                    }
                    else
                    {
                        sb.Append($"\t\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0) this.{ConvertToPascalCase(property.Key)}.ForEach(item => sb.Append(\"    \").Append(item).Append(\",\\n\"));\n");
                    }
                    sb.Append("\t\t\t\tsb.Append(\"  }\\n\");\n");
                }
                else
                {
                    if (property.Value.Ref != null)
                    {
                        sb.Append($"\t\t\t\tvar sb{property.Key} = new StringBuilder();\n");
                        sb.Append($"\t\t\t\tsb{property.Key}.Append(\"  {property.Key}: {{\\n\");\n");
                        sb.Append($"\t\t\t\tif (this.{ConvertToPascalCase(property.Key)} != null)\n");
                        sb.Append("\t\t\t\t{\n");
                        sb.Append($"\t\t\t\t\tsb{property.Key}.Append(this.{ConvertToPascalCase(property.Key)});\n");
                        sb.Append($"\t\t\t\t\tsb{property.Key}.Replace(\"\\n\", \"\\n    \");\n");
                        sb.Append($"\t\t\t\t\tsb{property.Key}.Append(\"\\n\");\n");
                        sb.Append("\t\t\t\t}\n");
                        sb.Append($"\t\t\t\tsb{property.Key}.Append(\"  }}\\n\");\n");
                        sb.Append($"\t\t\t\tsb.Append(sb{property.Key});\n");
                    }
                    else
                    {
                        sb.Append($"\t\t\t\tsb.Append($\"  {property.Key}: {{this.{ConvertToPascalCase(property.Key)}}}\\n\");\n");
                    }
                }
            }
            sb.Append("\t\t\t\tsb.Append(\"}\");\n");
            sb.Append("\t\t\t\treturn sb.ToString();\n");
            sb.Append("\t\t\t}\n");
        }

        private static void GenerateArg(StringBuilder sb, Dictionary<string, object[]> ps, Dictionary<string, string[]> enums, string funcName)
        {
            var usingDefaultParams = new Dictionary<string, Property?>();
            ps.ToList().ForEach(p =>
            {
                if (p.Value[1] is Property prop)
                {
                    string rawType = "";
                    bool nullable = false;
                    bool isIdOrDate = false;
                    if (prop.Type == null)
                    {
                        rawType = "JsonNode";
                    }
                    else if (prop.Type is JsonArray ja)
                    {
                        if (ja != null && ja.Count > 0 && ja[0] != null) rawType = ja[0].ToString();
                        nullable = true;
                    }
                    else if (prop.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                    {
                        rawType = s;
                    }
                    else
                    {
                        var st = prop.Type.ToString();
                        rawType = prop.Type != null && st != null ? st : "";
                    }
                    if (prop.Format == "date-time")
                    {
                        rawType = "DateTime";
                    }
                    if (rawType == "array")
                    {
                        if (prop.Items?.Type != null)
                        {
                            rawType = $"List<{prop.Items.Type}>";
                            if (prop.Items.Ref != null)
                            {
                                var Ref = prop.Items.Ref.Replace("#/components/schemas/", "");
                                rawType = $"List<Model.{Ref}>";
                            }
                            if (prop.Items != null && prop.Items.Items != null && prop.Items.Items.Type != null)
                            {
                                rawType = $"List<List<{prop.Items.Items.Type}>>";
                            }
                        }
                        else
                        {
                            rawType = "List<JsonNode>";
                        }
                    }
                    if (rawType == "number")
                    {
                        rawType = "decimal";
                    }
                    if (rawType == "boolean")
                    {
                        rawType = "bool";
                    }
                    if (rawType == "integer")
                    {
                        rawType = "int";
                    }
                    if (p.Key == "untilId" || p.Key == "sinceId" || p.Key == "untilDate" || p.Key == "sinceDate")
                    {
                        nullable = true;
                        isIdOrDate = true;
                        usingDefaultParams.Add(p.Key, new Property()
                        {
                            Type = JsonSerializer.Deserialize<JsonNode>($"[\"{rawType}\",\"null\"]"),
                            Default = null,
                        });
                    }
                    if (nullable)
                    {
                        rawType += "?";
                    }
                    if (rawType == "object") rawType = "JsonNode";
                    if ((prop.Default != null || nullable || rawType == "array") && !isIdOrDate)
                    {
                        usingDefaultParams.Add(p.Key, p.Value[1] as Property);
                    }
                    else if (prop != null && !isIdOrDate)
                    {
                        if (prop.Format != null && prop.Format == "binary")
                        {
                            rawType = "Stream";
                        }
                        if (prop.Enum != null)
                        {
                            enums.Add(ConvertToPascalCase(funcName) + ConvertToPascalCase(p.Key), prop.Enum);
                            rawType = $"{ConvertToPascalCase(funcName)}{ConvertToPascalCase(p.Key)}Enum";
                        }
                        sb.Append($"{rawType} {p.Key}");
                        if (!p.Equals(ps.ToList().Last()) || usingDefaultParams.Count != 0) sb.Append(",");
                    }
                }
            });

            usingDefaultParams.ToList().ForEach(p =>
            {
                if (p.Value != null)
                {
                    var prop = p.Value;
                    var defaultValue = $"{prop.Default}";
                    string rawType = "";
                    bool nullable = false;
                    if (prop.Type == null)
                    {
                        rawType = "JsonNode";
                    }
                    else if (prop.Type is JsonArray ja)
                    {
                        if (ja != null && ja.Count > 0 && ja[0] != null) rawType = ja[0].ToString();
                        nullable = true;
                    }
                    else if (prop.Type is JsonValue jv)
                    {
                        jv.AsValue().TryGetValue<string>(out var s);
                        rawType = s;
                    }
                    else
                    {
                        var st = prop.Type.ToString();
                        rawType = prop.Type != null && st != null ? st : "";
                    }
                    if (nullable || rawType == "array") defaultValue = "null";
                    if (rawType == "boolean")
                    {
                        defaultValue = JsonNamingPolicy.CamelCase.ConvertName($"{prop.Default}");
                        if (defaultValue == "" || defaultValue == "\"\"") defaultValue = "null";
                    }
                    if (rawType == "string")
                    {
                        defaultValue = $"\"{prop.Default}\"";
                        if (defaultValue == "" || defaultValue == "\"\"") defaultValue = "null";
                    }
                    if (rawType == "array")
                    {
                        if (prop.Items?.Type != null)
                        {
                            rawType = $"List<{prop.Items.Type}>";
                            if (prop.Items.Ref != null)
                            {
                                var Ref = prop.Items.Ref.Replace("#/components/schemas/", "");
                                rawType = $"List<Model.{Ref}>";
                            }
                            if (prop.Items != null && prop.Items.Items != null && prop.Items.Items.Type != null)
                            {
                                rawType = $"List<List<{prop.Items.Items.Type}>>";
                            }
                        }
                        else
                        {
                            rawType = "List<JsonNode>";
                        }
                    }
                    if (prop.Enum != null)
                    {
                        enums.Add(ConvertToPascalCase(funcName) + ConvertToPascalCase(p.Key), prop.Enum);
                        rawType = $"{ConvertToPascalCase(funcName) + ConvertToPascalCase(p.Key)}Enum";
                        if (defaultValue != "null")
                        {
                            var v = new StringBuilder(ConvertToPascalCase(Regex.Replace(defaultValue.Replace("\"", ""), @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus"))).Replace("@", "At");
                            defaultValue = $"{ConvertToPascalCase(funcName) + ConvertToPascalCase(p.Key)}Enum.{v}";
                        }
                    }
                    if (prop.Format == "date-time")
                    {
                        rawType = "DateTime";
                    }
                    if (rawType == "number")
                    {
                        rawType = "decimal";
                    }
                    if (rawType == "boolean")
                    {
                        rawType = "bool";
                    }
                    if (rawType == "integer")
                    {
                        rawType = "int";
                    }
                    if (rawType == "object") rawType = "JsonNode";
                    sb.Append($"{rawType}{(nullable || defaultValue == "null" ? "?" : "")} {p.Key} = {defaultValue}");
                    if (!p.Equals(usingDefaultParams.ToList().Last())) sb.Append(",");
                }
            });
        }

        private static void GenerateParamDictionaryCode(StringBuilder sb, Dictionary<string, object[]> ps)
        {
            sb.Append("\t\t\tvar param = new Dictionary<string, object?>\t\n");
            sb.Append("\t\t\t{\n");
            ps.ToList().ForEach(p => sb.Append($"\t\t\t\t{{ \"{p.Key}\", {p.Key} }},\n"));
            sb.Append("\t\t\t};\n");
        }

        public static string GenerateRequestCode(ApiDocument jsonNode, KeyValuePair<string, List<string>> pathTrees, string ns)
        {
            var sb = new StringBuilder();
            sb.Append("using Misharp;\n");
            sb.Append("using Misharp.Model;\n");
            sb.Append("using System.Text;\n");
            sb.Append("using System.Text.Json.Nodes;");
            sb.Append($"namespace {ns} {{\n");
            sb.Append($"\tpublic class {ConvertToPascalCase(pathTrees.Key)}Api {{\n");
            sb.Append("\t\tprivate Misharp.App _app;\n");
            var thirdClassName = new Dictionary<string, List<string>>();
            pathTrees.Value.ForEach(path =>
            {
                var pathMethod = jsonNode.Paths[$"/{path}"][ApiDocument.HttpMethod.Post];
                var trees = pathMethod.Summary.Split("/");
                // パスが3階層以上の場合は別途クラスを作るように
                if (trees.Length >= 3)
                {
                    if (!thirdClassName.ContainsKey(trees[1])) thirdClassName[trees[1]] = new List<string> { pathMethod.Summary };
                    else thirdClassName[trees[1]].Add(pathMethod.Summary);
                }
            });
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\tpublic {ConvertToPascalCase(pathTrees.Key)}.{ConvertToPascalCase(className)}Api {ConvertToPascalCase(className)}Api;\n");
            });
            sb.Append($"\t\tpublic {ConvertToPascalCase(pathTrees.Key)}Api(Misharp.App app)\n");
            sb.Append("\t\t{\n");
            sb.Append("\t\t\t_app = app;\n");
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\t\t{ConvertToPascalCase(className)}Api = new {ConvertToPascalCase(pathTrees.Key)}.{ConvertToPascalCase(className)}Api(_app);\n");
            });
            sb.Append("\t\t}\n");
            var enums = new Dictionary<string, string[]>();
            foreach (var pathTree in pathTrees.Value)
            {
                if (pathTree.StartsWith("i/2fa")) continue;
                Console.WriteLine($"Generating {pathTree} Controller");
                enums = new Dictionary<string, string[]>();
                var pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                var trees = ConvertToPascalCase(pathMethod.Summary).Split("/");
                var nothingReturn = false;
                Schema? responseSchema = null;
                if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                    responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                else if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.NoContent))
                    nothingReturn = true;
                var Ref = "";
                var ps = new Dictionary<string, object[]>();
                var needParam = false;
                if (pathMethod.RequestBody != null)
                {
                    needParam = true;
                    Schema? requestBodySchema = null;
                    if (pathMethod.RequestBody.Content.ContainsKey("application/json"))
                        requestBodySchema = pathMethod.RequestBody.Content["application/json"].Schema;
                    else if (pathMethod.RequestBody.Content.ContainsKey("multipart/form-data"))
                        requestBodySchema = pathMethod.RequestBody.Content["multipart/form-data"].Schema;
                    requestBodySchema?.Properties?.ToList().ForEach(property =>
                        {
                            string type = "";
                            if (property.Value.Type == null)
                            {
                                type = "JsonNode";
                            }
                            else if (property.Value.Type is JsonValue jv)
                            {
                                jv.AsValue().TryGetValue<string>(out var s);
                                type = s;
                            }
                            else if (property.Value.Type is JsonArray ja)
                            {
                                if (ja != null && ja.Count > 0 && ja[0] != null) type = ja[0].ToString();
                            }
                            else
                            {
                                var st = property.Value.Type.ToString();
                                type = property.Value.Type != null && st != null ? st : "";
                            }
                            if (type == "array")
                            {
                                if (property.Value.Items != null && property.Value.Items.Type != null)
                                {
                                    string itemsType = "JsonNode";
                                    var nullableItem = false;
                                    if (property.Value.Type is JsonValue jv)
                                    {
                                        jv.AsValue().TryGetValue<string>(out var s);
                                        type = s;
                                    }
                                    else if (property.Value.Type is JsonArray ja)
                                    {
                                        ja.AsValue().TryGetValue<string[]>(out var ar);
                                        if (ar != null && ar.Length > 0)
                                        {
                                            if (ar[0] != null)
                                            {
                                                itemsType = ar[0].ToString();
                                            }
                                            else if (ar[1] != null && ar[1] == "null")
                                            {
                                                nullableItem = true;
                                            }
                                        }
                                    }
                                    type = $"List<{itemsType}>";
                                    if (nullableItem)
                                    {
                                        type += "?";
                                    }
                                    if (property.Value.Items.Ref != null)
                                    {
                                        var Ref = property.Value.Items.Ref.Replace("#/components/schemas/", "");
                                        type = $"List<Model.{Ref}>";
                                    }
                                    if (property.Value.Items != null && property.Value.Items.Items != null && property.Value.Items.Items.Type != null)
                                    {
                                        string itemsItemType = "";
                                        var nullableItemsItem = false;
                                        if (property.Value.Type is JsonValue jv2)
                                        {
                                            jv2.AsValue().TryGetValue<string>(out var s);
                                            if (s != null) itemsItemType = s;
                                        }
                                        else if (property.Value.Type is JsonArray ja)
                                        {
                                            ja.AsValue().TryGetValue<string[]>(out var ar);
                                            if (ar != null && ar.Length > 0)
                                            {
                                                if (ar[0] != null)
                                                {
                                                    itemsItemType = ar[0].ToString();
                                                }
                                                else if (ar[1] != null && ar[1] == "null")
                                                {
                                                    nullableItemsItem = true;
                                                }
                                            }
                                        }
                                        type = $"List<List<{property.Value.Items.Items.Type}>>";
                                        if (nullableItemsItem)
                                        {
                                            type += "?";
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
                                property.Value.Properties?.Values.ToList().ForEach(p => Console.WriteLine(GetPropertyType(p)));
                                type = "JsonNode";
                            }
                            ps.Add(property.Key, new object[] { type, property.Value });
                        });
                }
                if (responseSchema != null && trees.Length <= 2)
                {
                    if (responseSchema.Ref != null)
                    {
                        Ref = responseSchema.Ref.Replace("#/components/schemas/", "");
                        var funcName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                funcName = ConvertToPascalCase(trees[0]);
                                sb.Append($"\t\tpublic async Task<Response<Model.{Ref}>> {ConvertToPascalCase(trees[0])}(");
                                break;
                            case 2:
                                funcName = ConvertToPascalCase(trees[1]);
                                sb.Append($"\t\tpublic async Task<Response<Model.{Ref}>> {ConvertToPascalCase(trees[1])}(");
                                break;
                            default:
                                break;
                        }
                        if (needParam) GenerateArg(sb, ps, enums, funcName);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tResponse<Model.{Ref}> result = await _app.Request<Model.{Ref}>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }

                    if (responseSchema.Properties != null)
                    {
                        var responseClassName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                responseClassName = ConvertToPascalCase(trees[0]);
                                break;
                            case 2:
                                responseClassName = ConvertToPascalCase(trees[0]) + ConvertToPascalCase(trees[1]);
                                break;
                            default:
                                break;
                        }
                        sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                        GeneratePropertiesCode(sb, responseSchema.Properties);
                        GenerateToStringCode(sb, responseSchema.Properties);
                        sb.Append("\t\t}\n");
                        var funcName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                funcName = ConvertToPascalCase(trees[0]);
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[0])}(");
                                break;
                            case 2:
                                funcName = ConvertToPascalCase(trees[1]);
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[1])}(");
                                break;
                            default:
                                break;
                        }
                        if (needParam) GenerateArg(sb, ps, enums, funcName);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tResponse<{responseClassName}Response> result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }

                    if (responseSchema.Items != null)
                    {
                        if (responseSchema.Items.Ref != null)
                            Ref = responseSchema.Items.Ref.Replace("#/components/schemas/", "");
                        sb.Append($"\t\tpublic async Task<Response<List<{GetPropertyType(responseSchema.Items)}>>> ");
                        var funcName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                funcName = ConvertToPascalCase(trees[0]);
                                sb.Append($"{ConvertToPascalCase(trees[0])}");
                                break;
                            case 2:
                                funcName = ConvertToPascalCase(trees[1]);
                                sb.Append($"{ConvertToPascalCase(trees[1])}");
                                break;
                            default:
                                break;
                        }
                        sb.Append("(");
                        if (needParam) GenerateArg(sb, ps, enums, funcName);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tResponse<List<{GetPropertyType(responseSchema.Items)}>> result = await _app.Request<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }

                    if (responseSchema != null && responseSchema.OneOf != null || responseSchema.AnyOf != null)
                    {
                        var responseProperties = ReturnAllPropertiesDictionary(responseSchema, jsonNode.Components["schemas"]);
                        var responseClassName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                responseClassName = ConvertToPascalCase(trees[0]);
                                break;
                            case 2:
                                responseClassName = ConvertToPascalCase(trees[0]) + ConvertToPascalCase(trees[1]);
                                break;
                            default:
                                break;
                        }
                        sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                        GeneratePropertiesCode(sb, responseProperties);
                        GenerateToStringCode(sb, responseProperties);
                        sb.Append("\t\t}\n");
                        var funcName = "";
                        switch (trees.Length)
                        {
                            case 1:
                                funcName = ConvertToPascalCase(trees[0]);
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[0])}(");
                                break;
                            case 2:
                                funcName = ConvertToPascalCase(trees[1]);
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[1])}(");
                                break;
                            default:
                                break;
                        }
                        if (needParam) GenerateArg(sb, ps, enums, funcName);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tResponse<{responseClassName}Response> result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }
                }
                else if (nothingReturn && trees.Length <= 2)
                {
                    sb.Append($"\t\tpublic async Task<Response<Model.EmptyResponse>> ");
                    var funcName = "";
                    switch (trees.Length)
                    {
                        case 1:
                            funcName = ConvertToPascalCase(trees[0]);
                            sb.Append($"{ConvertToPascalCase(trees[0])}");
                            break;
                        case 2:
                            funcName = ConvertToPascalCase(trees[1]);
                            sb.Append($"{ConvertToPascalCase(trees[1])}");
                            break;
                        default:
                            break;
                    }
                    sb.Append("(");
                    if (needParam) GenerateArg(sb, ps, enums, funcName);
                    sb.Append(")\n");
                    sb.Append("\t\t{\n");
                    if (needParam) GenerateParamDictionaryCode(sb, ps);
                    sb.Append($"\t\t\tvar result = await _app.Request<Model.EmptyResponse>(\"{pathTree}\", ");
                    if (needParam) sb.Append("param, ");
                    sb.Append("successStatusCode: System.Net.HttpStatusCode.NoContent, ");
                    sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                    sb.Append("\t\t\treturn result;\n");
                    sb.Append("\t\t}\n");
                }
                if (enums.Count > 0)
                {
                    foreach (var item in enums)
                    {
                        sb.Append($"\t\tpublic enum {ConvertToPascalCase(item.Key)}Enum {{\n");
                        foreach (var value in item.Value)
                        {
                            if (value != null)
                            {
                                var v = new StringBuilder(ConvertToPascalCase(Regex.Replace(value, @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus")));
                                sb.Append($"\t\t\t[StringValue(\"{value}\")]\n");
                                sb.Append($"\t\t\t{v.Replace("@", "At").ToString()},\n");
                            }
                        }
                        sb.Append("\t\t}\n");
                    }
                }
            }
            sb.Append("\t}\n");
            enums = new Dictionary<string, string[]>();
            if (thirdClassName.Count > 0)
            {
                sb.Append("}\n");
                sb.Append($"namespace {ns}.{ConvertToPascalCase(pathTrees.Key)} {{\n");
                thirdClassName.ToList().ForEach(classNames =>
                {
                    sb.Append($"\tpublic class {ConvertToPascalCase(classNames.Key)}Api\n");
                    sb.Append("\t{\n");
                    sb.Append("\t\tprivate Misharp.App _app;\n");
                    sb.Append($"\t\tpublic {ConvertToPascalCase(classNames.Key)}Api(Misharp.App app)\n");
                    sb.Append("\t\t{\n");
                    sb.Append("\t\t\t_app = app;\n");
                    sb.Append("\t\t}\n");
                    foreach (string pathTree in classNames.Value)
                    {
                        enums = new Dictionary<string, string[]>();
                        var pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                        var trees = ConvertToPascalCase(pathMethod.Summary).Split("/");
                        var nothingReturn = false;
                        Schema? responseSchema = null;
                        if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                            responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                        else if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.NoContent))
                            nothingReturn = true;
                        var Ref = "";
                        var ps = new Dictionary<string, object[]>();
                        var needParam = false;
                        var useForm = false;
                        if (pathMethod.RequestBody != null)
                        {
                            needParam = true;
                            Schema? requestBodySchema = null;
                            if (pathMethod.RequestBody.Content.ContainsKey("application/json"))
                                requestBodySchema = pathMethod.RequestBody.Content["application/json"].Schema;
                            else if (pathMethod.RequestBody.Content.ContainsKey("multipart/form-data"))
                            {
                                requestBodySchema = pathMethod.RequestBody.Content["multipart/form-data"].Schema;
                                useForm = true;
                            }
                            requestBodySchema?.Properties?.ToList().ForEach(property =>
                            {
                                string type = "";
                                if (property.Value.Type == null)
                                {
                                    type = "JsonNode";
                                }
                                else if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                                {
                                    type = s;
                                }
                                else if (property.Value.Type is JsonArray ja)
                                {
                                    if (ja != null && ja.Count > 0 && ja[0] != null) type = ja[0].ToString();
                                }
                                if (type == "array")
                                {
                                    if (property.Value.Items != null && property.Value.Items.Type != null)
                                    {
                                        type = $"List<{property.Value.Items.Type}>";
                                        if (property.Value.Items.Ref != null)
                                        {
                                            var Ref = property.Value.Items.Ref.Replace("#/components/schemas/", "");
                                            type = $"List<Model.{Ref}>";
                                        }
                                        if (property.Value.Items != null && property.Value.Items.Items != null && property.Value.Items.Items.Type != null)
                                        {
                                            type = $"List<List<{property.Value.Items.Items.Type}>>";
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
                                if (type == "object") type = "JsonNode";
                                ps.Add(property.Key, new object[] { type, property.Value });
                            });
                        }
                        if (responseSchema != null)
                        {
                            if (responseSchema.Ref != null)
                            {
                                Ref = responseSchema.Ref.Replace("#/components/schemas/", "");
                                sb.Append($"\t\tpublic async Task<Response<Model.{Ref}>> {ConvertToPascalCase(trees[2])}(");
                                if (needParam) GenerateArg(sb, ps, enums, ConvertToPascalCase(trees[2]));
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<Model.{Ref}>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<Model.{Ref}>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }

                            if (responseSchema.Properties != null)
                            {
                                var responseClassName = "";
                                responseClassName = ConvertToPascalCase(trees[0]) + ConvertToPascalCase(trees[1]) + ConvertToPascalCase(trees[2]);
                                sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                                GeneratePropertiesCode(sb, responseSchema.Properties);
                                GenerateToStringCode(sb, responseSchema.Properties);
                                sb.Append("\t\t}\n");
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[2])}(");
                                if (needParam) GenerateArg(sb, ps, enums, ConvertToPascalCase(trees[2]));
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<{responseClassName}Response>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }

                            if (responseSchema.Items != null)
                            {
                                if (responseSchema.Items.Ref != null)
                                    Ref = responseSchema.Items.Ref.Replace("#/components/schemas/", "");
                                sb.Append($"\t\tpublic async Task<Response<List<{GetPropertyType(responseSchema.Items)}>>> ");
                                sb.Append($"{ConvertToPascalCase(trees[2])}");
                                sb.Append("(");
                                if (needParam) GenerateArg(sb, ps, enums, ConvertToPascalCase(trees[2]));
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }

                            if (responseSchema != null && responseSchema.OneOf != null || responseSchema.AnyOf != null)
                            {
                                var responseProperties = ReturnAllPropertiesDictionary(responseSchema, jsonNode.Components["schemas"]);
                                var responseClassName = "";
                                responseClassName = ConvertToPascalCase(trees[0]) + ConvertToPascalCase(trees[1]) + ConvertToPascalCase(trees[2]);
                                sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                                GeneratePropertiesCode(sb, responseProperties);
                                GenerateToStringCode(sb, responseProperties);
                                sb.Append("\t\t}\n");
                                sb.Append($"\t\tpublic async Task<Response<{responseClassName}Response>> {ConvertToPascalCase(trees[2])}(");
                                if (needParam) GenerateArg(sb, ps, enums, ConvertToPascalCase(trees[2]));
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<{responseClassName}Response>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }
                        }
                        else if (nothingReturn)
                        {
                            sb.Append($"\t\tpublic async Task<Response<Model.EmptyResponse>> {ConvertToPascalCase(trees[2])}(");
                            if (needParam) GenerateArg(sb, ps, enums, ConvertToPascalCase(trees[2]));
                            sb.Append(")\n");
                            sb.Append("\t\t{\n");
                            if (needParam) GenerateParamDictionaryCode(sb, ps);
                            sb.Append($"\t\t\tvar result = await _app.Request<Model.EmptyResponse>(\"{pathTree}\", ");
                            if (needParam) sb.Append("param, ");
                            sb.Append("successStatusCode: System.Net.HttpStatusCode.NoContent, ");
                            sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
                            sb.Append("\t\t\treturn result;\n");
                            sb.Append("\t\t}\n");
                        }
                    }
                    sb.Append("\t}\n");
                });
                if (enums.Count > 0)
                {
                    foreach (var item in enums)
                    {
                        sb.Append($"\t\tpublic enum {ConvertToPascalCase(item.Key)}Enum {{\n");
                        foreach (var value in item.Value)
                        {
                            if (value != null)
                            {
                                var v = new StringBuilder(ConvertToPascalCase(Regex.Replace(value, @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus")));
                                v.Replace("@", "At");
                                sb.Append($"\t\t\t[StringValue(\"{value}\")]\n");
                                sb.Append($"\t\t\t{v.ToString()},\n");
                            }
                        }
                        sb.Append("\t\t}\n");
                    }
                }
            }
            sb.Append("}");
            var code = sb.ToString();
            return code;
        }
    }
}
