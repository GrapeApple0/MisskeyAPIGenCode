using LibOpenApiGen;
using System;
using System.Diagnostics;
namespace OpenApiGenCode
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string json = "";
            //using (var http = new HttpClient())
            //{
            //    var res = await http.GetAsync("https://misskey.04.si/api.json");
            //    json = await res.Content.ReadAsStringAsync();
            //}
            Console.Write("Are you sure you want to regenerate api.json? [y/N] ");
            var response = Console.ReadKey(false).Key;
            Console.WriteLine();
            if (response == ConsoleKey.Y)
            {
                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = @".\gen-api-json.bat",
                    CreateNoWindow = false,
                });
                process!.OutputDataReceived += (s, e) =>
                {
                    Console.WriteLine(e.Data);
                };
                process?.WaitForExit();
            }
            if (File.Exists("api.json"))
            {
                json = File.ReadAllText("api.json");
                Generator.GenerateCode(json);
            }
        }
    }
}