using System.Text.RegularExpressions;
using Flurl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public class FlowElement
{
    private static readonly Regex PlaceholderRegex = new(@".*(?'placeholder'{{(?'key'\w+)}}).*", RegexOptions.Compiled);
    [JsonIgnore]
    public JObject Value => GetJsonValue();
    public ParsedRequest Request { get; private set; }
    public ParsedResponse Response { get; private set; }
    private JsonBinding RequestBody { get; set; }
    private JsonBinding ResponseBody { get; set; }
    public Dictionary<string, Export> Exports = [];
    [JsonConstructor]
    public FlowElement(ParsedRequest request, ParsedResponse response, Dictionary<string, Export> exports)
    {
        Request = request;
        Response = response;
        Exports = exports;

        GenerateBindings();
    }

    public FlowElement(ParsedRequest request, ParsedResponse response)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));

        Response = response ?? throw new ArgumentNullException(nameof(response));

        GenerateBindings();
    }

    private void GenerateBindings()
    {
        string requestContentType = Utils.ContentType(Request.Headers, Request.Body);
        RequestBody = new JsonBinding(Request.Body, requestContentType switch
        {
            "application/json" => ContentType.JSON,
            "text/html" => ContentType.XML,
            "application/xml" => ContentType.XML,
            "text/plain" => ContentType.TEXT,
            _ => ContentType.TEXT
        });

        string responseContentType = Utils.ContentType(Response.Headers, Response.Body);
        ResponseBody = new JsonBinding(Response.Body, responseContentType switch
        {
            "application/json" => ContentType.JSON,
            "application/xml" => ContentType.XML,
            "text/html" => ContentType.TEXT,
            "text/plain" => ContentType.TEXT,
            _ => ContentType.TEXT
        });
    }

    private JObject GetJsonValue()
    {
        Console.WriteLine(JToken.FromObject(Request));
        Console.WriteLine(JToken.FromObject(Response));
        return new JObject
        {
            ["Request"] = new JObject
            {
                ["Url"] = Request.Url.RemoveQuery().ToString(),
                ["Headers"] = JToken.FromObject(Request.Headers ?? []) as JObject,
                ["QueryParameters"] = JToken.FromObject(Request.QueryParams ?? []) as JObject,
                ["Cookies"] = JToken.FromObject(Request.Cookies ?? []) as JObject,
                ["Body"] = RequestBody.Value ?? "",
            },
            ["Response"] = new JObject
            {
                ["Status"] = Response.Status,
                ["Headers"] = JToken.FromObject(Response.Headers ?? []) as JObject,
                ["Cookies"] = JToken.FromObject(Response.Cookies ?? []) as JObject,
                ["Body"] = ResponseBody.Value ?? ""
            }
        };
    }

    public void UpdateRequest(JObject requestJson)
    {
        Request.Url = requestJson["Url"].Value<string>();
        Request.Headers = (requestJson["Headers"] as JObject).ToObject<Dictionary<string, string>>();
        Request.QueryParams = (requestJson["QueryParameters"] as JObject).ToObject<Dictionary<string, string>>();
        Request.Cookies = (requestJson["Cookies"] as JObject).ToObject<Dictionary<string, string>>();
        Request.Body = requestJson["Body"].ToString();
    }

    public void UpdateResponse(ParsedResponse response)
    {
        Response = response;
        GenerateBindings();
    }

    public void FillRequestPlaceholders(Dictionary<string, JToken> variables)
    {
        JObject request = Value["Request"] as JObject;
        foreach (JValue stringToken in request.Descendants().OfType<JValue>().Where(jv => jv.Type is JTokenType.String))
        {
            string value = stringToken.Value.ToString();
            var match = PlaceholderRegex.Match(value);
            if (match.Success && match.Groups["placeholder"].Value == value)
            {
                string key = match.Groups["key"].Value;
                if (variables.ContainsKey(key))
                {
                    stringToken.Replace(variables[key]);
                }
            }
            else
            {
                string result = PlaceholderRegex.Replace(value, match =>
                {
                    string key = match.Groups["key"].Value;
                    string placeholder = match.Groups["placeholder"].Value;
                    return match.Value.Replace(placeholder, variables.ContainsKey(key) ? variables[key].ToString() : placeholder);
                });
                stringToken.Value = result;
            }
        }
        UpdateRequest(request);
    }

    public void AddExport(string name, Export export)
    {
        Exports.Add(name, export);
        Console.WriteLine($"Added export with path {export.JsonPath}");
    }

    public Dictionary<string, JToken> GetExported()
    {
        Dictionary<string, JToken> exported = new();
        foreach (KeyValuePair<string, Export> export in Exports)
        {
            exported[export.Key] = export.Value.GetValue(Value);
        }
        return exported;
    }

    public override string ToString()
    {
        return $"{Value}\n\nExports: {JToken.FromObject(Exports)}";
    }

}

public class Export
{
    public string JsonPath { get; private set; }
    public string Regex { get; private set; }
    [JsonConstructor]
    public Export(string jsonpath, string regex = "")
    {
        JsonPath = jsonpath;
        Regex = regex;
    }
    public JToken GetValue(JObject source)
    {
        JToken selected = Utils.SelectByJPath(source, JsonPath);
        if (Regex != string.Empty)
        {
            selected = new Regex(Regex).Match(selected.ToString()).Value;
        }
        return selected;
    }
}

public enum HttpMethod
{
    GET,
    POST,
    PUT,
    DELETE,
    PATCH,
    OPTIONS,
    HEAD,
    /*
    not needed for now
    CONNECT,
    TRACE
    */
}