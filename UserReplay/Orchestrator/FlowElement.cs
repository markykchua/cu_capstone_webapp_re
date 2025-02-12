using System.Text.RegularExpressions;
using System.Globalization;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace UserReplay
{
    [Serializable]
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

        //Parsed Request Segments
        public string Url { get; set; }
        public HttpMethod Method { get; set; }
        public Dictionary<string, string> QueryParams { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public ParsedResponse Response { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan CallDuration { get; set; }
        public string RequestVersion { get; set; }
        public Dictionary<string, string> Cookies { get; set; }

        public FlowElement(Url url, HttpMethod method, Dictionary<string, string> queryParams, Dictionary<string, string> headers, string body, DateTime startTime, TimeSpan callDuration, string requestVersion, Dictionary<string, string> cookies)
        {
            Url = url;
            Method = method;
            QueryParams = queryParams;
            Headers = headers;
            Body = body;
            StartTime = startTime;
            CallDuration = callDuration;
            RequestVersion = requestVersion;
            Cookies = cookies;
        }

        public FlowElement(JObject request, JObject response)
        {
            Url = request["url"].Value<string>();
            Method = Enum.Parse<HttpMethod>(request["method"].Value<string>());
            Headers = request.ContainsKey("headers") ? (request["headers"] as JArray).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
            QueryParams = request.ContainsKey("queryString") ? (request["queryString"] as JArray).DistinctBy(h => h["name"]).ToDictionary(q => q["name"].Value<string>(), q => q["value"].Value<string>()) : new Dictionary<string, string>();
            Response = new ParsedResponse(response);
            Body = request.ContainsKey("postData") ? request["postData"]["text"].Value<string>() : "";
            Cookies = (request["cookies"] as JArray).ToDictionary(c => c["name"].Value<string>(), c => c["value"].Value<string>());
            RequestVersion = request["httpVersion"].Value<string>();
        }

        public void SetTime(string startTimeString, double callDuration)
        {
            StartTime = DateTime.Parse(startTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            CallDuration = TimeSpan.FromMilliseconds(callDuration);
        }

        internal static readonly string[] supportedHttpVersions = ["1.0", "1.1", "2.0", "3.0"];
        public async Task<IFlurlResponse> Replay()
        {
            IFlurlRequest request = new FlurlRequest(Url).AllowAnyHttpStatus();
            string[] splitHttpVersion = RequestVersion.Split("/");
            if (splitHttpVersion.Length > 1 && supportedHttpVersions.Contains(splitHttpVersion[1]))
            {
                request.WithSettings(s => s.HttpVersion = splitHttpVersion[1]);
            }
            else
            {
                request.WithSettings(s => s.HttpVersion = "1.1");
            }
            foreach (var header in Headers)
            {
                // skip headers that are not allowed with Flurl
                if (header.Key.StartsWith(":") || header.Key.Equals("content-length", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                else
                {
                    request.WithHeader(header.Key, header.Value);
                }
            }
            foreach (var query in QueryParams)
            {
                request.SetQueryParam(query.Key, query.Value);
            }
            return Method switch
            {
                HttpMethod.GET => await request.GetAsync(),
                HttpMethod.POST => await request.PostStringAsync(Body),
                HttpMethod.PUT => await request.PutStringAsync(Body),
                HttpMethod.PATCH => await request.PatchStringAsync(Body),
                HttpMethod.OPTIONS => await request.OptionsAsync(),
                HttpMethod.DELETE => await request.DeleteAsync(),
                _ => throw new InvalidOperationException()
            };
        }

        public static Regex numberIdSegment = new(@"\d+", RegexOptions.Compiled);
        public static Regex uuidSegment = new(@"[a-f0-9]{8}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{12}", RegexOptions.Compiled);
        public static Regex urlSegmentMatcher = new(@"(\w+)", RegexOptions.Compiled);
        public string UrlTemplate()
        {
            string withoutQuery = new Uri(Url).AbsolutePath;

            // Replace numeric ID segments with {previous_segment}_id
            withoutQuery = numberIdSegment.Replace(withoutQuery, match =>
            {
                var previousSegment = GetPreviousSegment(withoutQuery, match.Index);
                return $"{previousSegment}_id";
            });

            // Replace UUID segments with {previous_segment}_uuid
            withoutQuery = uuidSegment.Replace(withoutQuery, match =>
            {
                var previousSegment = GetPreviousSegment(withoutQuery, match.Index);
                return $"{previousSegment.TrimEnd('s')}_uuid";
            });

            return withoutQuery;
        }
        private string GetPreviousSegment(string path, int matchIndex)
        {
            var segments = path.Substring(0, matchIndex).Split('/');
            return segments.Length > 1 ? segments[^2] : "id";
        }

        /**
            * public override string ToString()
        {
            return $"REQUEST {Method} {Url} - {JObject.FromObject(QueryParams)}{(Method != HttpMethod.GET && !string.IsNullOrEmpty(Body) ? $"\nBody: {Body}" : "")} \n==>\n{Response}\n";
        }
            */

            //Parsed Response Start
            public int ResponseStatus { get; set; }
            public Dictionary<string, string> ResponseHeaders { get; set; }
            // already exists
            //public string ResponseBody { get; set; }
            public Dictionary<string, string> ResponseCookies { get; set; }

            public string ResponseToString()
        {
            return $"RESPONSE {ResponseStatus} : {(!string.IsNullOrEmpty(ResponseBody.Value.ToString()) ? $"\nBody: {(ResponseBody.Value.ToString().Length > 200 ? ResponseBody.Value[..200] + "......" : ResponseBody.Value)}" : "")}\n";
        }

        public int GetResponseHashCode()
        {
            return HashCode.Combine(ResponseStatus, ResponseHeaders, ResponseBody);
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