using LibOpenApiGen;
namespace OpenApiGenCode
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string json = "";
            using (var http = new HttpClient())
            {
                var res = await http.GetAsync("https://misskey.04.si/api.json");
                json = await res.Content.ReadAsStringAsync();
            }
            //json = File.ReadAllText("api.json");
            Generator.GenerateCode(json);
        }
    }
}