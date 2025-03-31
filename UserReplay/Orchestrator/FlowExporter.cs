using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public static class FlowExporter
{
    public static void ExportFlowToTxt(UserFlow userFlow, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("\n=== External Variables ===");
            foreach (var variable in userFlow.ExternalVariables)
            {
                writer.WriteLine($"{variable.Key}: {variable.Value.ToString(Formatting.None)}");
            }

            writer.WriteLine("=== Flow Elements ===");
            foreach (var flowElement in userFlow.FlowElements)
            {
                writer.WriteLine("Request:");
                writer.WriteLine($"URL: {flowElement.Request.Url}");
                writer.WriteLine($"Method: {flowElement.Request.Method}");
                writer.WriteLine($"Query Params: {JObject.FromObject(flowElement.Request.QueryParams).ToString(Formatting.None)}");
                writer.WriteLine($"Headers: {JObject.FromObject(flowElement.Request.Headers).ToString(Formatting.None)}");
                writer.WriteLine($"Cookies: {JObject.FromObject(flowElement.Request.Cookies).ToString(Formatting.None)}");
                if (!string.IsNullOrEmpty(flowElement.Request.Body))
                {
                    writer.WriteLine($"Body: {flowElement.Request.Body}");
                }

                writer.WriteLine(new string('-', 50));
            }
        }

        Console.WriteLine($"Flow and external variables exported to {filePath}");
    }

    public static void ExportFlowToJson(UserFlow userFlow, string filePath)
    {
        var exportData = new JObject
        {
            ["ExternalVariables"] = JObject.FromObject(userFlow.ExternalVariables),
            ["Requests"] = new JArray(
                userFlow.FlowElements.Select(flowElement => new JObject
                {
                    ["Url"] = flowElement.Request.Url,
                    ["Method"] = flowElement.Request.Method.ToString(),
                    ["QueryParams"] = JObject.FromObject(flowElement.Request.QueryParams),
                    ["Headers"] = JObject.FromObject(flowElement.Request.Headers),
                    ["Cookies"] = JObject.FromObject(flowElement.Request.Cookies),
                    ["Body"] = flowElement.Request.Body
                }))
        };

        File.WriteAllText(filePath, exportData.ToString(Formatting.Indented));
        Console.WriteLine($"Flow and external variables exported to {filePath}");
    }
}