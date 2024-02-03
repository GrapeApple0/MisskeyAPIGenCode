using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text;

namespace LibOpenApiGen
{
    public static class Generator
    {
        public static string GenerateCode(string raw)
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            ApiDocument? jsonNode = JsonSerializer.Deserialize<ApiDocument>(new StringBuilder(raw).Replace("$ref", "ref").ToString(), options);
            if (jsonNode == null) return "";

            string? code;
            // Models
            foreach (var component in jsonNode.Components["schemas"].Keys.ToList())
            {
                code = Modeller.GenerateModelCode(jsonNode.Components["schemas"], component, "Misharp.Model");
                string path = $"./Models/{component}.cs";
                new FileInfo(path).Directory?.Create();
                File.WriteAllText(path, code);
            }

            var pathTrees = new Dictionary<string, List<string>>();
            foreach (var path in jsonNode.Paths)
            {
                var trees = path.Value[ApiDocument.HttpMethod.Post].OperationId.Split("/");
                if (!pathTrees.ContainsKey(trees[0])) pathTrees[trees[0]] = new List<string> { path.Value[ApiDocument.HttpMethod.Post].OperationId };
                else pathTrees[trees[0]].Add(path.Value[ApiDocument.HttpMethod.Post].OperationId);
            }
            var constructApis = new StringBuilder();
            var apis = new StringBuilder();
            foreach (var item in pathTrees)
            {
                if (item.Key != "admin" && item.Key != "charts")
                {
                    code = Controller.GenerateRequestCode(jsonNode, item, "Misharp.Controls");
                    string path = $"./Controls/{ApiDocument.ConvertToPascalCase(item.Key)}.cs";
                    new FileInfo(path).Directory?.Create();
                    File.WriteAllText(path, code);
                    apis.Append($"\tpublic {ApiDocument.ConvertToPascalCase(item.Key).Replace("-", "")}Api {ApiDocument.ConvertToPascalCase(item.Key).Replace("-", "")}Api {{ get; }}\n");
                    constructApis.Append($"\t\tthis.{ApiDocument.ConvertToPascalCase(item.Key).Replace("-", "")}Api = new {ApiDocument.ConvertToPascalCase(item.Key).Replace("-", "")}Api(this);\n");
                }
            }
            return "";
        }
    }
}