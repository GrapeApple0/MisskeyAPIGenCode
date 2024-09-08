using System.Text;
using System.Text.RegularExpressions;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
    public static class Modeller
    {
        private static bool useList = false;

        public static string GenerateModelCode(Dictionary<string, Schema> root, string model, string ns)
        {
            var component = root[model];
            string code;
            var sb = new StringBuilder();
            sb.Append("using System.Text.Json;\n");
            sb.Append("using System.Text.Json.Nodes;\n");
            sb.Append("using System.Text;\n");
            sb.Append($"namespace {ns} {{\n");
            if (Regex.IsMatch(model, "^[0-9]")) model = "_" + model;
            model = Shared.ConvertToPascalCase(model);
            var sb2 = new StringBuilder();
            var baseModels = new List<string>();
            if (component.Properties != null)
            {
                Shared.GeneratePropertiesCode(sb2, component.Properties, model);
                Shared.GenerateToStringCode(sb2, component.Properties, model);
            }
            else if (component.OneOf != null || component.AllOf != null)
            {
                var properties = Shared.ReturnAllPropertiesDictionary(component, root, out baseModels);
                Shared.GeneratePropertiesCode(sb2, properties, model);
                Shared.GenerateToStringCode(sb2, properties, model);
            }
            sb.Append($"\tpublic class {model} ");
            if (baseModels.Count == 1)
            {
                sb.Append($": {baseModels[0]} ");
            }
            // C#は多重継承を受け入れていないので2つ以上になるとこれらのクラスのさらに基底クラスの生成が必要
            sb.Append("{\n");
            sb.Append(sb2);
            sb.Append("\t}\n");
            sb.Append("}\n");
            if (useList) sb.Insert(0, "using System.Collections.Generic;\n");
            code = sb.ToString();
            return code;
        }
    }
}