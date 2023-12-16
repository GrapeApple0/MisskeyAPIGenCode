using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
    public static class Controller
    {
        private static string GetPropertyType(Property property)
        {
            string type;
            if (property.Ref != null)
            {
                var Ref = property.Ref.Replace("#/components/schemas/", "");
                type = $"{Ref}";
            }
            else
            {
                type = $"{property.Type.ToLower()}";
            }
            if (property.Type == "array")
            {
                useList = true;
                if (property.Items == null) return "";
                if (property.Items.Type != null)
                {
                    type = $"List<{property.Items.Type}>";
                    if (property.Items.Ref != null)
                    {
                        var Ref = property.Items.Ref.Replace("#/components/schemas/", "");
                        type = $"List<{Ref}>";
                    }
                    if (property.Items != null && property.Items.Items != null && property.Items.Items.Type != null)
                    {
                        type = $"List<{property.Items.Items.Type}>";
                    }
                }
            }
            if (property.Format == "date-time")
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
            if (type == "integer")
            {
                type = "int";
            }
            if (property.Nullable.GetValueOrDefault(false) || property.Optional.GetValueOrDefault(false))
            {
                type += "?";
            }
            return type;
        }

        private static bool useList = false;
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
                if (property.Value.Type == "array")
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
                        // sb.Append($"\t\t\tsb.Append(\"  {property.Key}: {{\\n\").Append(\"    \").Append(this.{Regex.Replace(property.Key, @"\b\p{Ll}", match => match.Value.ToUpper())}).Replace(\"\\n\", \"\\n    \").Append(\"\\n\").Append(\"  }}");
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

        private static void GenerateArg(StringBuilder sb, Dictionary<string, object[]> ps, Dictionary<string, string[]> enums)
        {
            var usingDefaultParams = new Dictionary<string, object[]>();
            ps.ToList().ForEach(p =>
            {
                if (p.Value[1] is Property prop)
                {
                    if (prop.Default != null || prop.Nullable.GetValueOrDefault(false) || prop.Type == "array")
                    {
                        usingDefaultParams.Add(p.Key, p.Value);
                    }
                    else
                    {
                        var type = p.Value[0]?.ToString();
                        if (prop.Format != null && prop.Format == "binary")
                        {
                            type = "Stream";
                        }
                        if (prop.Enum != null)
                        {
                            enums.Add(p.Key, prop.Enum);
                            type = $"{ConvertToPascalCase(p.Key)}Enum";
                        }
                        sb.Append($"{type} {p.Key}");
                        if (!p.Equals(ps.ToList().Last()) || usingDefaultParams.Count != 0) sb.Append(",");
                    }
                }
            });
            usingDefaultParams.ToList().ForEach(p =>
            {
                if (p.Value[1] is Property prop)
                {
                    var defaultValue = $"{prop.Default}";
                    var type = p.Value[0]?.ToString();
                    if (prop.Nullable.GetValueOrDefault(false) || prop.Type == "array") defaultValue = "null";
                    if (prop.Type == "boolean")
                    {
                        defaultValue = JsonNamingPolicy.CamelCase.ConvertName($"{prop.Default}");
                        if (defaultValue == "" || defaultValue == "\"\"") defaultValue = "null";
                    }
                    if (prop.Type == "string")
                    {
                        defaultValue = $"\"{prop.Default}\"";
                        if (defaultValue == "" || defaultValue == "\"\"") defaultValue = "null";
                    }
                    if (prop.Enum != null)
                    {
                        enums.Add(p.Key, prop.Enum);
                        type = $"{ConvertToPascalCase(p.Key)}Enum";
                        if (defaultValue != "null")
                        {
                            var v = new StringBuilder(ConvertToPascalCase(Regex.Replace(defaultValue.Replace("\"", ""), @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus"))).Replace("-", "");
                            defaultValue = $"{ConvertToPascalCase(p.Key)}Enum.{v}";
                        }
                    }
                    sb.Append($"{type}{(prop.Nullable.GetValueOrDefault(false) || prop.Type == "array" ? "?" : "")} {p.Key} = {defaultValue}");
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
            sb.Append("using Misharp.Model;\n");
            sb.Append("using System.Text;\n");
            sb.Append($"namespace {ns} {{\n");
            sb.Append($"\tpublic class {ConvertToPascalCase(pathTrees.Key).Replace("-", "")}Api {{\n");
            sb.Append("\t\tprivate Misharp.App _app;\n");
            var thirdClassName = new Dictionary<string, List<string>>();
            pathTrees.Value.ForEach(path =>
            {
                var pathMethod = jsonNode.Paths[$"/{path}"][ApiDocument.HttpMethod.Post];
                var trees = pathMethod.OperationId.Split("/");
                if (trees.Length >= 3)
                {
                    if (!thirdClassName.ContainsKey(trees[1])) thirdClassName[trees[1]] = new List<string> { pathMethod.OperationId };
                    else thirdClassName[trees[1]].Add(pathMethod.OperationId);
                }
            });
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\tpublic {ConvertToPascalCase(pathTrees.Key)}.{ConvertToPascalCase(className).Replace("-", "")}Api {ConvertToPascalCase(className).Replace("-", "")}Api;\n");
            });
            sb.Append($"\t\tpublic {ConvertToPascalCase(pathTrees.Key).Replace("-", "")}Api(Misharp.App app)\n");
            sb.Append("\t\t{\n");
            sb.Append("\t\t\t_app = app;\n");
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\t\t{ConvertToPascalCase(className).Replace("-", "")}Api = new {ConvertToPascalCase(pathTrees.Key)}.{ConvertToPascalCase(className).Replace("-", "")}Api(_app);\n");
            });
            sb.Append("\t\t}\n");
            var enums = new Dictionary<string, string[]>();
            foreach (var pathTree in pathTrees.Value)
            {
                enums = new Dictionary<string, string[]>();
                var pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                var trees = pathMethod.OperationId.Replace("-", "").Split("/");
                PathsMethod.Schema? responseSchema = null;
                if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                    responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                var Ref = "";
                var ps = new Dictionary<string, object[]>();
                var needParam = false;
                if (pathMethod.RequestBody != null)
                {
                    needParam = true;
                    PathsMethod.Schema? requestBodySchema = null;
                    if (pathMethod.RequestBody.Content.ContainsKey("application/json"))
                        requestBodySchema = pathMethod.RequestBody.Content["application/json"].Schema;
                    else if (pathMethod.RequestBody.Content.ContainsKey("multipart/form-data"))
                        requestBodySchema = pathMethod.RequestBody.Content["multipart/form-data"].Schema;
                    requestBodySchema?.Properties?.ToList().ForEach(property =>
                        {
                            var type = property.Value.Type;
                            if (property.Value.Type == "array")
                            {
                                if (property.Value.Items != null && property.Value.Items.Type != null)
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
                            if (property.Value.Format == "date-time") type = "DateTime";
                            if (type == "number") type = "decimal";
                            if (type == "integer") type = "int";
                            if (type == "boolean") type = "bool";
                            ps.Add(property.Key, new object[] { type, property.Value });
                        });
                }
                if (responseSchema != null && trees.Length <= 2)
                {
                    if (responseSchema.Ref != null)
                    {
                        Ref = responseSchema.Ref.Replace("#/components/schemas/", "");
                        switch (trees.Length)
                        {
                            case 1:
                                sb.Append($"\t\tpublic async Task<{Ref}> {ConvertToPascalCase(trees[0])}(");
                                break;
                            case 2:
                                sb.Append($"\t\tpublic async Task<{Ref}> {ConvertToPascalCase(trees[1])}(");
                                break;
                            default:
                                break;
                        }
                        if (needParam) GenerateArg(sb, ps, enums);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tvar result = await _app.Request<{Ref}>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
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
                        switch (trees.Length)
                        {
                            case 1:
                                sb.Append($"\t\tpublic async Task<{responseClassName}Response> {ConvertToPascalCase(trees[0])}(");
                                break;
                            case 2:
                                sb.Append($"\t\tpublic async Task<{responseClassName}Response> {ConvertToPascalCase(trees[1])}(");
                                break;
                            default:
                                break;
                        }
                        if (needParam) GenerateArg(sb, ps, enums);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tvar result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }

                    if (responseSchema.Items != null)
                    {
                        if (responseSchema.Items.Ref != null)
                            Ref = responseSchema.Items.Ref.Replace("#/components/schemas/", "");
                        sb.Append($"\t\tpublic async Task<List<{GetPropertyType(responseSchema.Items)}>> ");
                        switch (trees.Length)
                        {
                            case 1:
                                sb.Append($"{ConvertToPascalCase(trees[0])}");
                                break;
                            case 2:
                                sb.Append($"{ConvertToPascalCase(trees[1])}");
                                break;
                            default:
                                break;
                        }
                        sb.Append("(");
                        if (needParam) GenerateArg(sb, ps, enums);
                        sb.Append(")\n");
                        sb.Append("\t\t{\n");
                        if (needParam) GenerateParamDictionaryCode(sb, ps);
                        sb.Append($"\t\t\tvar result = await _app.Request<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                        if (needParam) sb.Append("param, ");
                        sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
                        sb.Append("\t\t\treturn result;\n");
                        sb.Append("\t\t}\n");
                    }
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
                                v.Replace("-", "");
                                sb.Append($"\t\t\t[StringValue(\"{value}\")]\n");
                                sb.Append($"\t\t\t{v.ToString()},\n");
                            }
                        }
                        sb.Append("\t\t}\n");
                    }
                }
            }
            sb.Append("\t}\n");
            if (thirdClassName.Count > 0)
            {
                sb.Append("}\n");
                sb.Append($"namespace {ns}.{ConvertToPascalCase(pathTrees.Key)} {{\n");
                thirdClassName.ToList().ForEach(classNames =>
                {
                    sb.Append($"\tpublic class {ConvertToPascalCase(classNames.Key).Replace("-", "")}Api\n");
                    sb.Append("\t{\n");
                    sb.Append("\t\tprivate Misharp.App _app;\n");
                    sb.Append($"\t\tpublic {ConvertToPascalCase(classNames.Key).Replace("-", "")}Api(Misharp.App app)\n");
                    sb.Append("\t\t{\n");
                    sb.Append("\t\t\t_app = app;\n");
                    sb.Append("\t\t}\n");
                    foreach (string pathTree in classNames.Value)
                    {
                        var pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                        var trees = pathMethod.OperationId.Replace("-", "").Split("/");
                        PathsMethod.Schema? responseSchema = null;
                        if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                            responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                        var Ref = "";
                        var ps = new Dictionary<string, object[]>();
                        var needParam = false;
                        var useForm = false;
                        if (pathMethod.RequestBody != null)
                        {
                            needParam = true;
                            PathsMethod.Schema? requestBodySchema = null;
                            if (pathMethod.RequestBody.Content.ContainsKey("application/json"))
                                requestBodySchema = pathMethod.RequestBody.Content["application/json"].Schema;
                            else if (pathMethod.RequestBody.Content.ContainsKey("multipart/form-data"))
                            {
                                requestBodySchema = pathMethod.RequestBody.Content["multipart/form-data"].Schema;
                                useForm = true;
                            }
                            requestBodySchema?.Properties?.ToList().ForEach(property =>
                            {
                                var type = property.Value.Type;
                                if (property.Value.Type == "array")
                                {
                                    if (property.Value.Items != null && property.Value.Items.Type != null)
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
                                if (property.Value.Format == "date-time") type = "DateTime";
                                if (type == "number") type = "decimal";
                                if (type == "integer") type = "int";
                                if (type == "boolean") type = "bool";
                                ps.Add(property.Key, new object[] { type, property.Value });
                            });
                        }

                        if (responseSchema != null)
                        {
                            if (responseSchema.Ref != null)
                            {
                                Ref = responseSchema.Ref.Replace("#/components/schemas/", "");
                                sb.Append($"\t\tpublic async Task<{Ref}> {ConvertToPascalCase(trees[2])}(");
                                if (needParam) GenerateArg(sb, ps, enums);
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<{Ref}>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<{Ref}>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
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
                                sb.Append($"\t\tpublic async Task<{responseClassName}Response> {ConvertToPascalCase(trees[2])}(");
                                if (needParam) GenerateArg(sb, ps, enums);
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<{responseClassName}Response>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<{responseClassName}Response>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }

                            if (responseSchema.Items != null)
                            {
                                if (responseSchema.Items.Ref != null)
                                    Ref = responseSchema.Items.Ref.Replace("#/components/schemas/", "");
                                sb.Append($"\t\tpublic async Task<List<{GetPropertyType(responseSchema.Items)}>> ");
                                sb.Append($"{ConvertToPascalCase(trees[2])}");
                                sb.Append("(");
                                if (needParam) GenerateArg(sb, ps, enums);
                                sb.Append(")\n");
                                sb.Append("\t\t{\n");
                                if (needParam) GenerateParamDictionaryCode(sb, ps);
                                if (useForm)
                                    sb.Append($"\t\t\tvar result = await _app.RequestFormData<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                                else
                                    sb.Append($"\t\t\tvar result = await _app.Request<List<{GetPropertyType(responseSchema.Items)}>>(\"{pathTree}\", ");
                                if (needParam) sb.Append("param, ");
                                sb.Append($"{(pathMethod.Security != null ? "true" : "false")});\n");
                                sb.Append("\t\t\treturn result;\n");
                                sb.Append("\t\t}\n");
                            }
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
                                v.Replace("-", "");
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
