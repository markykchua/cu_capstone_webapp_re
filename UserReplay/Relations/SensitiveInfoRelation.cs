using System.Composition;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public class SensitiveInfoRelation : IFlowRelation
{
    public static string UsernameVariableName = "extracted_username";
    public static string PasswordVariableName = "extracted_password";

    // Add more sensitive keys here as needed
    private static readonly List<string> SensitiveKeys = new List<string>
    {
        "username",
        "password",
        "email",
        "csrfmiddlewaretoken",
        "cookie"
        
    };

    public void InsertRelation(UserFlow flow)
    {
        int sensitiveCount = 0;

        foreach (FlowElement flowElement in flow.FlowElements)
        {
            var body = flowElement.Request.Body;
            if (string.IsNullOrEmpty(body))
            {
                continue;
            }

            

            foreach (var key in SensitiveKeys)
            {
                var value = ExtractValue(body, key);
                if (!string.IsNullOrEmpty(value))
                {
                    Console.WriteLine($"Original body: {body}");

                    sensitiveCount++;
                    flow.ExternalVariables[$"extracted_{key}_"] = JToken.FromObject(value);
                    Console.WriteLine($"Found sensitive info ({key}) in request to {flowElement.Request.Url}");

                    // Replace the sensitive value in the request body with a placeholder
                    body = body.Replace(value, $"{{{{extracted_{key}}}}}");

                    flowElement.Request.Body = body;
                    Console.WriteLine($"Altered body: {body}");
                }
            }

            
        }

        Console.WriteLine($"Found {sensitiveCount} sensitive relations in the flow");
    }

    private string ExtractValue(string body, string key)
    {
        // Check if the body is JSON
        if (body.TrimStart().StartsWith("{"))
        {
            var regex = new Regex($"\"{key}\":\"(.*?)\"");
            var match = regex.Match(body);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        // Check if the body is URL-encoded form data
        else if (body.Contains("="))
        {
            var regex = new Regex($"{key}=(.*?)(&|$)");
            var match = regex.Match(body);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        return string.Empty;
    }
}