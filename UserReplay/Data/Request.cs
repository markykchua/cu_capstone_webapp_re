using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace UserReplay
{

    [Serializable]
    public class ParsedRequest
    {
        public string Url { get; set; }
        public HttpMethod Method { get; set; }
        public Dictionary<string, string> QueryParams { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan CallDuration { get; set; }
        public string RequestVersion { get; set; }
        public Dictionary<string, string> Cookies { get; set; }

        [Newtonsoft.Json.JsonConstructorAttribute]
        public ParsedRequest(Url url, HttpMethod method, Dictionary<string, string> queryParams, Dictionary<string, string> headers, string body, ParsedResponse response, DateTime startTime, TimeSpan callDuration, string requestVersion, Dictionary<string, string> cookies)
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

        public ParsedRequest(JObject request)
        {
            Url = request["url"].Value<string>();
            Method = Enum.Parse<HttpMethod>(request["method"].Value<string>());
            Headers = request.ContainsKey("headers") ? (request["headers"] as JArray).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
            QueryParams = request.ContainsKey("queryString") ? (request["queryString"] as JArray).DistinctBy(h => h["name"]).ToDictionary(q => q["name"].Value<string>(), q => q["value"].Value<string>()) : new Dictionary<string, string>();
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

        public override string ToString()
        {
            return $"REQUEST {Method} {Url} - {JObject.FromObject(QueryParams)}{(Method != HttpMethod.GET && !string.IsNullOrEmpty(Body) ? $"\nBody: {Body}" : "")}\n";
        }
    }

}