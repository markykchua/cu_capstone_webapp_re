using System.Text.RegularExpressions;
using Flurl;
using Newtonsoft.Json.Linq;

namespace UserReplay
{
    public class FlowElement
    {
        private static readonly Regex PlaceholderRegex = new(@".*({{(\w+)}}).*", RegexOptions.Compiled);
        public JObject Value => GetJsonValue();
        public ParsedRequest Request { get; private set; }
        private JsonBinding RequestBody { get; set; }
        private JsonBinding ResponseBody { get; set; }
        private Dictionary<string, Export> Exports = [];

        public FlowElement(FlowElement r)
        {

        }
        public FlowElement(ParsedRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));

            Request = request;
            string requestContentType = Utils.ContentType(request.Headers, request.Body);

            RequestBody = new JsonBinding(request.Body, requestContentType switch
            {
                "application/json" => ContentType.JSON,
                "text/html" => ContentType.XML,
                "application/xml" => ContentType.XML,
                "text/plain" => ContentType.TEXT,
                _ => ContentType.TEXT
            });
            
            string responseContentType = Utils.ContentType(request.Response.Headers, request.Response.Body);
            ResponseBody = new JsonBinding(request.Response.Body, responseContentType switch
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
            //Console.WriteLine(JToken.FromObject(Request));
            return new JObject
            {
                ["Request"] = new JObject
                {
                    ["Url"] = Request.Url.RemoveQuery().ToString(),
                    ["Headers"] = JToken.FromObject(Request.Headers) as JObject,
                    ["QueryParameters"] = JToken.FromObject(Request.QueryParams) as JObject,
                    ["Cookies"] = JToken.FromObject(Request.Cookies) as JObject,
                    ["Body"] = RequestBody.Value,
                },
                ["Response"] = new JObject
                {
                    ["Status"] = Request.Response.Status,
                    ["Headers"] = JToken.FromObject(Request.Response.Headers) as JObject,
                    ["Cookies"] = JToken.FromObject(Request.Response.Cookies) as JObject,
                    ["Body"] = ResponseBody.Value
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
            Request.Response = response;
        }

        public void FillRequestPlaceholders(Dictionary<string, JToken> variables)
        {
            JObject request = Value["Request"] as JObject;
            foreach (JValue stringToken in request.Descendants().OfType<JValue>().Where(jv => jv.Type is JTokenType.String))
            {
                string value = stringToken.Value.ToString();
                var match = PlaceholderRegex.Match(value);
                if (match.Success && match.Groups[1].Value == value)
                {
                    string key = match.Groups[2].Value;
                    if (variables.ContainsKey(key))
                    {
                        stringToken.Replace(variables[key]);
                    }
                }
                else
                {
                    string result = PlaceholderRegex.Replace(value, match =>
                    {
                        string key = match.Groups[2].Value;
                        return variables.ContainsKey(key) ? variables[key].ToString() : match.Value;
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
        public Export(string path, string regex = "")
        {
            JsonPath = path;
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
}