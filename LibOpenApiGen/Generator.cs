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
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                PropertyNameCaseInsensitive = true
            };
            ApiDocument? jsonNode = JsonSerializer.Deserialize<ApiDocument>(new StringBuilder(raw).Replace("$ref", "ref").Replace("\u001a", "").ToString(), options);
            if (jsonNode == null) return "";
            Console.WriteLine($"Api.json's Misskey Version: {jsonNode.Info["version"]}");
            string? code;
            if (Directory.Exists("./Models")) Directory.Delete("./Models", true);
            // Models
            foreach (var component in jsonNode.Components["schemas"].Keys.ToList())
            {
                if (component == "Error" || component == "ReversiGameLite" || component == "ReversiGameDetailed") continue;
                Console.WriteLine($"Generating {component} Models");
                code = Modeller.GenerateModelCode(jsonNode.Components["schemas"], component, "Misharp.Model");
                string path = $"./Models/{component}.cs";
                new FileInfo(path).Directory?.Create();
                File.WriteAllText(path, code);
            }
            if (Directory.Exists("./Controls")) Directory.Delete("./Controls", true);
            // Controls
            var pathTrees = new Dictionary<string, List<string>>();
            foreach (var path in jsonNode.Paths)
            {
                var trees = path.Value[ApiDocument.HttpMethod.Post].Summary.Split("/");
                if (!pathTrees.ContainsKey(trees[0])) pathTrees[trees[0]] = new List<string> { path.Value[ApiDocument.HttpMethod.Post].Summary };
                else pathTrees[trees[0]].Add(path.Value[ApiDocument.HttpMethod.Post].Summary);
            }
            var constructApis = new StringBuilder();
            var apis = new StringBuilder();
            foreach (var item in pathTrees)
            {
                if (item.Key != "admin" && item.Key != "charts" && item.Key != "page-push" && item.Key != "test" && item.Key != "reversi" && item.Key != "bubble-game")
                {
                    code = Controller.GenerateRequestCode(jsonNode, item, "Misharp.Controls");
                    string path = $"./Controls/{ApiDocument.ConvertToPascalCase(item.Key)}.cs";
                    new FileInfo(path).Directory?.Create();
                    File.WriteAllText(path, code);
                    apis.Append($"\tpublic {ApiDocument.ConvertToPascalCase(item.Key)}Api {ApiDocument.ConvertToPascalCase(item.Key)}Api {{ get; }}\n");
                    constructApis.Append($"\t\tthis.{ApiDocument.ConvertToPascalCase(item.Key)}Api = new {ApiDocument.ConvertToPascalCase(item.Key)}Api(this);\n");
                }
            }
            Console.WriteLine(apis.ToString());
            Console.WriteLine("################################");
            Console.WriteLine(constructApis.ToString());
            return "";
        }
    }
}