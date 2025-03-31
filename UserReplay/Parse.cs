using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UserReplay;

[Verb("parse", HelpText = "Parse HAR file into readable format")]
public partial class Parse : Verb
{
    [Option('f', "file", Required = true, HelpText = "Path to the HAR file")] public required string FileName { get; set; }
    [Option('o', Required = false, HelpText = "Export to OpenAPI spec")] public string SwaggerFile { get; set; }

    internal static readonly string[] openApiIgnoreHeaders = ["accept", "content-type", "authorization"];

    [GeneratedRegex(@"/\{(\w+)\}/", RegexOptions.Compiled)]
    private static partial Regex PathParameterRegex();

    public override Task<bool> Run()
    {
        try
        {
            if (!File.Exists(FileName))
            {
                Log.Error("File does not exist");
                return Task.FromResult(false);
            }
            else
            {
                Log.Information($"Parsing HAR file: {FileName}");
                // parse HAR file into JObject
                var har = JObject.Parse(File.ReadAllText(FileName));
                UserFlow session = UserFlow.FromHar(har);

                foreach (var element in session.FlowElements)
                {
                    Log.Information(element.ToString());
                }

                Log.Information(session.ToString());

                if (!string.IsNullOrEmpty(SwaggerFile))
                {

                    Log.Information($"Writing OpenAPI spec to: {SwaggerFile}");
                    var openApiSpec = Utils.GenerateOpenApiSpec(session);
                    File.WriteAllText(SwaggerFile, openApiSpec);
                    Log.Information($"OpenAPI spec written to: {SwaggerFile}");
                }

            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred");
            return Task.FromResult(false);
        }
    }

}

public static class JTokenExtensions
{
    public static bool TryParse(this string input, out JToken value)
    {
        try
        {
            value = JToken.Parse(input);
            return true;
        }
        catch
        {
            value = JValue.CreateNull();
            return false;
        }
    }
}