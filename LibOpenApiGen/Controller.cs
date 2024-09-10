using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
    public static class Controller
    {
        private static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties)
        {
            string type;
            string name;
            foreach (var property in properties)
            {
                var propertyType = Shared.GetPropertyType(property.Value, sb, Shared.ConvertToPascalCase(property.Key), "", 2);
                name = Shared.ConvertToPascalCase(property.Key); 
                type = propertyType.Type;
                var nullable = propertyType.Nullable;
                sb.Append($"\t\t\tpublic {type} {name} {{ get; set; }}\n");
            }
        }

        private static void GenerateArg(StringBuilder sb, Dictionary<string, object[]> ps, 
                                        ref Dictionary<string, string[]> needEnums, string funcName, 
                                        out StringBuilder paramObjectClass)
        {
            var usingDefaultParams = new Dictionary<string, Property?>();
            var sb2 = new StringBuilder();
            var enums = new Dictionary<string, string[]>();
            ps.ToList().ForEach(p =>
            {
                if (p.Value[1] is Property prop)
                {
                    string rawType = "";
                    bool nullable = false;
                    bool isIdOrDate = false;
                    if (prop.Type == null) rawType = "JsonNode";
                    else if (prop.Type is JsonArray ja)
                    {
                        if (ja != null && ja.Count > 0) rawType = ja[0]?.ToString() ?? "";
                        nullable = true;
                    }
                    else if (prop.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
                        rawType = s;
                    else
                    {
                        var st = prop.Type.ToString();
                        rawType = prop.Type != null && st != null ? st : "";
                    }
                    if (prop.Format == "date-time") rawType = "DateTime";
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
                                rawType = $"List<List<{prop.Items.Items.Type}>>";
                        }
                        else rawType = "List<JsonNode>";
                        nullable = true;
                    }
                    rawType = Shared.ConvertJavaScriptTypeToCSharpType(rawType);
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
                    if (rawType == "object")
                    {
                        rawType = "JsonNode";
                        if (prop.Properties != null)
                        {
                            sb2.Append($"\t\tpublic class {Shared.ConvertToPascalCase(funcName.Replace("/", "-"))}{Shared.ConvertToPascalCase(p.Key)}ParamObject {{\n");
                            Shared.GeneratePropertiesCode(sb2, prop.Properties, "", 2);
                            Shared.GenerateToStringCode(sb2, prop.Properties, indent: 3);
                            sb2.Append("\t\t}\n");
                            rawType = $"{Shared.ConvertToPascalCase(funcName.Replace("/", "-"))}{Shared.ConvertToPascalCase(p.Key)}ParamObject";
                        }
                    }
                    if (nullable) rawType += "?";
                    if (!isIdOrDate)
                    {
                        if (prop.Default != null || nullable || rawType == "array")
                            usingDefaultParams.Add(p.Key, p.Value[1] as Property);
                        else if (prop != null)
                        {
                            if (prop.Format != null && prop.Format == "binary") rawType = "Stream";
                            if (prop.Enum != null)
                            {
                                enums.Add(Shared.ConvertToPascalCase(funcName) + Shared.ConvertToPascalCase(p.Key), prop.Enum);
                                rawType = $"{Shared.ConvertToPascalCase(funcName)}{Shared.ConvertToPascalCase(p.Key)}Enum";
                            }
                            sb.Append($"{rawType} {p.Key}");
                            if (!p.Equals(ps.ToList().Last()) || usingDefaultParams.Count != 0) sb.Append(",");
                        }
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
                    if (prop.Type == null) rawType = "JsonNode";
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
                        else rawType = "List<JsonNode>";
                    }
                    if (prop.Enum != null)
                    {
                        enums.Add(Shared.ConvertToPascalCase(funcName) + Shared.ConvertToPascalCase(p.Key), prop.Enum);
                        rawType = $"{Shared.ConvertToPascalCase(funcName) + Shared.ConvertToPascalCase(p.Key)}Enum";
                        if (defaultValue != "null")
                        {
                            var v = new StringBuilder(Shared.ConvertToPascalCase(Regex.Replace(defaultValue.Replace("\"", ""), @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus"))).Replace("@", "At");
                            defaultValue = $"{Shared.ConvertToPascalCase(funcName) + Shared.ConvertToPascalCase(p.Key)}Enum.{v}";
                        }
                    }
                    if (prop.Format == "date-time") rawType = "DateTime";
                    rawType = Shared.ConvertJavaScriptTypeToCSharpType(rawType);
                    sb.Append($"{rawType}{(nullable || defaultValue == "null" ? "?" : "")} {p.Key} = {defaultValue}");
                    if (!p.Equals(usingDefaultParams.ToList().Last())) sb.Append(",");
                }
            });
            paramObjectClass = sb2;
            needEnums = enums.Concat(needEnums.Where(pair =>
                !enums.ContainsKey(pair.Key))).ToDictionary(k => k.Key, v => v.Value);
        }

        private static void GenerateParamDictionaryCode(StringBuilder sb, Dictionary<string, object[]> ps)
        {
            sb.Append("\t\t\tvar param = new Dictionary<string, object?>\t\n");
            sb.Append("\t\t\t{\n");
            ps.ToList().ForEach(p => sb.Append($"\t\t\t\t{{ \"{p.Key}\", {p.Key} }},\n"));
            sb.Append("\t\t\t};\n");
        }

        private static void GenerateFunction(StringBuilder sb, Dictionary<string, object[]> ps,
                                                PathsMethod pathMethod, string pathTree,
                                                ref Dictionary<string, string[]> enums,
                                                bool needParam, string funcName,
                                                string responseType, bool useForm = false
                                            )
        {
            sb.Append($"\t\tpublic async Task<Response<{responseType}>> {funcName}");
            sb.Append("(");
            var paramObjectClass = new StringBuilder();
            if (needParam) GenerateArg(sb, ps, ref enums, funcName, out paramObjectClass);
            sb.Append(")\n");
            sb.Append("\t\t{\n");
            //if (list.Count > 0) list.ForEach(l => sb.Append($"\t\t\t{l} ??= new();\n"));
            if (needParam) GenerateParamDictionaryCode(sb, ps);
            sb.Append($"\t\t\tvar result = await _app.Request{(useForm ? "FormData" : "")}<{responseType}>(\"{pathTree}\", ");
            if (needParam) sb.Append("param, ");
            sb.Append("successStatusCode: System.Net.HttpStatusCode.NoContent, ");
            sb.Append($"useToken: {(pathMethod.Security != null ? "true" : "false")});\n");
            sb.Append("\t\t\treturn result;\n");
            sb.Append("\t\t}\n");
            sb.Append(paramObjectClass);
        }

        private static void GenerateEnums(StringBuilder sb, Dictionary<string, string[]> enums)
        {
            foreach (var item in enums)
            {
                sb.Append($"\t\tpublic enum {Shared.ConvertToPascalCase(item.Key)}Enum {{\n");
                foreach (var value in item.Value)
                {
                    if (value != null)
                    {
                        var v = new StringBuilder(Shared.ConvertToPascalCase(Regex.Replace(value, @"^[+-]", match => match.Value == "+" ? "Plus" : "Minus")));
                        v.Replace("@", "At");
                        sb.Append($"\t\t\t[StringValue(\"{value}\")]\n");
                        sb.Append($"\t\t\t{v.ToString()},\n");
                    }
                }
                sb.Append("\t\t}\n");
            }
        }

        private static string GetResponseType(Schema? responseSchema, ApiDocument jsonNode,
                                              StringBuilder sb, string responseClassName, bool nothingReturn)
        {
            var responseType = "";
            if (responseSchema != null) // 返り値がある場合
            {
                if (responseSchema.Type != null)
                {
                    var rawType = responseSchema.Type.ToString();
                    responseType = Shared.ConvertJavaScriptTypeToCSharpType(rawType);
                    if (rawType == "object") responseType = "JsonNode";
                }
                if (responseSchema.Ref != null) // モデルを参照する場合
                {
                    var _ref = responseSchema.Ref.Replace("#/components/schemas/", "");
                    responseType = $"Model.{_ref}";
                }
                if (responseSchema.Properties != null) // 新しくプロパティ用のクラスを作る場合
                {
                    sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                    GeneratePropertiesCode(sb, responseSchema.Properties);
                    Shared.GenerateToStringCode(sb, responseSchema.Properties, indent: 3);
                    sb.Append("\t\t}\n");
                    responseType = $"{responseClassName}Response";
                }
                if (responseSchema.Items != null)
                    responseType = $"List<{Shared.GetPropertyType(responseSchema.Items, sb, "Response", responseClassName + "Item").Type}>";
                if (responseSchema.OneOf != null || responseSchema.AnyOf != null || responseSchema.AllOf != null)
                {
                    var responseProperties = Shared.ReturnAllPropertiesDictionary(responseSchema, jsonNode.Components["schemas"]);
                    sb.Append($"\t\tpublic class {responseClassName}Response {{\n");
                    GeneratePropertiesCode(sb, responseProperties);
                    Shared.GenerateToStringCode(sb, responseProperties, indent: 3);
                    sb.Append("\t\t}\n");
                    responseType = $"{responseClassName}Response";
                }
            }
            else if (nothingReturn) responseType = "Model.EmptyResponse";
            return responseType;
        }

        public static string GenerateRequestCode(ApiDocument jsonNode, KeyValuePair<string, List<string>> pathTrees, string ns)
        {
            var sb = new StringBuilder();
            sb.Append("using Misharp;\n");
            sb.Append("using Misharp.Model;\n");
            sb.Append("using System.Text;\n");
            sb.Append("using System.Text.Json.Nodes;\n");
            sb.Append($"namespace {ns} {{\n");
            sb.Append($"\tpublic class {Shared.ConvertToPascalCase(pathTrees.Key)}Api {{\n");
            sb.Append("\t\tprivate Misharp.App _app;\n");
            var thirdClassName = new Dictionary<string, List<string>>();
            foreach (var pathTree in pathTrees.Value)
            {
                // 2FA関連のパスは除外
                if (pathTree.StartsWith("i/2fa")) continue;
                if (pathTree.StartsWith("drive/files/check-existence")) continue;
                var pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                var trees = pathMethod.Summary.Split("/");
                // パスが3階層以上の場合は別途クラスを作るように
                if (trees.Length >= 3)
                {
                    if (!thirdClassName.ContainsKey(trees[1])) 
                        thirdClassName[trees[1]] = new List<string> { pathMethod.Summary };
                    else thirdClassName[trees[1]].Add(pathMethod.Summary);
                }
            }
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\tpublic {Shared.ConvertToPascalCase(pathTrees.Key)}.{Shared.ConvertToPascalCase(className)}Api {Shared.ConvertToPascalCase(className)}Api;\n");
            });
            sb.Append($"\t\tpublic {Shared.ConvertToPascalCase(pathTrees.Key)}Api(Misharp.App app)\n");
            sb.Append("\t\t{\n");
            sb.Append("\t\t\t_app = app;\n");
            thirdClassName.Keys.ToList().ForEach(className =>
            {
                sb.Append($"\t\t\t{Shared.ConvertToPascalCase(className)}Api = new {Shared.ConvertToPascalCase(pathTrees.Key)}.{Shared.ConvertToPascalCase(className)}Api(_app);\n");
            });
            sb.Append("\t\t}\n");
            var enums = new Dictionary<string, string[]>();
            foreach (var pathTree in pathTrees.Value)
            {
                if (pathTree.StartsWith("i/2fa")) continue;
                enums = new Dictionary<string, string[]>();
                PathsMethod pathMethod;
                if (jsonNode.Paths[$"/{pathTree}"].ContainsKey(ApiDocument.HttpMethod.Post))
                    pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                else continue;
                var trees = Shared.ConvertToPascalCase(pathMethod.Summary).Split("/");
                var nothingReturn = false;
                Schema? responseSchema = null;
                if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                    responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                else if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.NoContent))
                    nothingReturn = true;
                var ps = new Dictionary<string, object[]>();
                var needParam = false;
                if (pathMethod.RequestBody != null) // 引数を取るかどうか
                {
                    needParam = true;
                    Schema? requestBodySchema = null;
                    if (pathMethod.RequestBody.Content.ContainsKey("application/json"))
                        requestBodySchema = pathMethod.RequestBody.Content["application/json"].Schema;
                    else if (pathMethod.RequestBody.Content.ContainsKey("multipart/form-data"))
                        requestBodySchema = pathMethod.RequestBody.Content["multipart/form-data"].Schema;
                    requestBodySchema?.Properties?.ToList().ForEach(property =>
                    {
                        var type = Shared.GetPropertyType(property.Value, sb, Shared.ConvertToPascalCase(property.Key), "", 2);
                        ps.Add(property.Key, new object[] { type.Type, property.Value });
                    });
                }
                if (trees.Length <= 2) // パスが2階層以下の場合
                {
                    Console.WriteLine($"Generating {pathTree} Controller");
                    var funcName = "";
                    var responseType = "";
                    funcName = Shared.ConvertToPascalCase(trees[^1]);
                    responseType = GetResponseType(responseSchema, jsonNode, sb, funcName, nothingReturn);
                    GenerateFunction(sb, ps, pathMethod,
                                     pathTree, ref enums, needParam,
                                     funcName, responseType);
                }
                if (enums.Count > 0) GenerateEnums(sb, enums);
            }
            sb.Append("\t}\n");
            enums = new Dictionary<string, string[]>();
            if (thirdClassName.Count > 0)
            {
                sb.Append("}\n");
                sb.Append($"namespace {ns}.{Shared.ConvertToPascalCase(pathTrees.Key)} {{\n");
                thirdClassName.ToList().ForEach(classNames =>
                {
                    sb.Append($"\tpublic class {Shared.ConvertToPascalCase(classNames.Key)}Api\n");
                    sb.Append("\t{\n");
                    sb.Append("\t\tprivate Misharp.App _app;\n");
                    sb.Append($"\t\tpublic {Shared.ConvertToPascalCase(classNames.Key)}Api(Misharp.App app)\n");
                    sb.Append("\t\t{\n");
                    sb.Append("\t\t\t_app = app;\n");
                    sb.Append("\t\t}\n");
                    foreach (string pathTree in classNames.Value)
                    {
                        Console.WriteLine($"Generating {pathTree} Controller");
                        enums = new Dictionary<string, string[]>();
                        PathsMethod pathMethod;
                        if (jsonNode.Paths[$"/{pathTree}"].ContainsKey(ApiDocument.HttpMethod.Post))
                            pathMethod = jsonNode.Paths[$"/{pathTree}"][ApiDocument.HttpMethod.Post];
                        else continue;
                        var trees = Shared.ConvertToPascalCase(pathMethod.Summary).Split("/");
                        var nothingReturn = false;
                        Schema? responseSchema = null;
                        if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.OK))
                            responseSchema = pathMethod.Responses[(int)System.Net.HttpStatusCode.OK].Content["application/json"].Schema;
                        else if (pathMethod.Responses.ContainsKey((int)System.Net.HttpStatusCode.NoContent))
                            nothingReturn = true;
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
                                var type = Shared.GetPropertyType(property.Value, sb, Shared.ConvertToPascalCase(property.Key), "");
                                ps.Add(property.Key, new object[] { type.Type, property.Value });
                            });
                        }
                        string funcName = Shared.ConvertToPascalCase(trees[2]);
                        string responseType = "";
                        var responseClassName = Shared.ConvertToPascalCase(trees[0]) + Shared.ConvertToPascalCase(trees[1]) + Shared.ConvertToPascalCase(trees[2]);
                        responseType = GetResponseType(responseSchema, jsonNode, sb, responseClassName, nothingReturn);
                        GenerateFunction(sb, ps, pathMethod, pathTree,
                                             ref enums, needParam, funcName, responseType, useForm);
                    }
                    sb.Append("\t}\n");
                });
                if (enums.Count > 0) GenerateEnums(sb, enums);
            }
            sb.Append('}');
            var code = sb.ToString();
            return code;
        }
    }
}