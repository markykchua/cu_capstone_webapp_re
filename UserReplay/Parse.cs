
using System.Globalization;
using System.Net;
using CommandLine;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UserReplay
{
    [Verb("parse", HelpText = "Parse HAR file into readable format")]
    class Parse : Verb
    {
        [Option('f', "file", Required = true, HelpText = "Path to the HAR file")] public required string FileName { get; set; }
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
                    Session session = new(har);

                    foreach (var request in session.Requests)
                    {
                        Log.Information(request.ToString());
                    }

                    Log.Information(session.ToString());

                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred");
                return Task.FromResult(false);
            }
        }

        public class ParsedRequest
        {
            public string Url { get; set; }
            public HttpMethod Method { get; set; }
            public Dictionary<string, string> QueryParams { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string Body { get; set; }
            public ParsedResponse Response { get; set; }
            public DateTime StartTime { get; set; }
            public TimeSpan CallDuration { get; set; }
            public string RequestVersion { get; set; }

            public ParsedRequest(JObject request, JObject response)
            {
                Url = request["url"].Value<string>();
                Method = Enum.Parse<HttpMethod>(request["method"].Value<string>());
                Headers = request.ContainsKey("headers") ? (request["headers"] as JArray).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
                QueryParams = request.ContainsKey("queryString") ? (request["queryString"] as JArray).ToDictionary(q => q["name"].Value<string>(), q => q["value"].Value<string>()) : new Dictionary<string, string>();
                Response = new ParsedResponse(response);
                Body = request.ContainsKey("postData") ? request["postData"]["text"].Value<string>() : "";
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

            public AuthInfo GetAuth()
            {
                AuthInfo authInfo = new();
                if (Headers.TryGetValue("authorization", out string value))
                {
                    if (Enum.TryParse(value.Split(" ")[0], true, out AuthType authType))
                    {
                        authInfo.Type = authType;
                    }
                    else
                    {
                        authInfo.Type = AuthType.Other;
                    }
                    authInfo.Credentials = value;
                }
                else
                {
                    authInfo.Type = AuthType.None;
                }
                return authInfo;
            }

            public override string ToString()
            {
                return $"REQUEST {Method} {Url} - {JObject.FromObject(QueryParams)}{(Method != HttpMethod.GET && !string.IsNullOrEmpty(Body) ? $"\nBody: {Body}" : "")} \n==>\n{Response}\n";
            }
        }


        public class ParsedResponse
        {
            public int Status { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string Body { get; set; }

            public ParsedResponse(JObject response)
            {
                Status = response["status"].Value<int>();
                Headers = response.ContainsKey("headers") ? (response["headers"] as JArray).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
                Body = response["content"]?["text"]?.Value<string>() ?? "";
            }
            public ParsedResponse(IFlurlResponse response)
            {
                Status = response.StatusCode;
                Headers = response.Headers.ToDictionary(h => h.Name, h => h.Value);
                Body = response.ResponseMessage.Content.ReadAsStringAsync().Result;
            }



            public override string ToString()
            {
                return $"RESPONSE {Status} : {(!string.IsNullOrEmpty(Body) ? $"\nBody: {(Body.Length > 200 ? Body[..200] + "......" : Body)}" : "")}\n";
            }

            public override bool Equals(object obj)
            {
                return obj is ParsedResponse response &&
                       Status == response.Status &&
                        EqualityComparer<Dictionary<string, string>>.Default.Equals(Headers, response.Headers) &&
                        Body.Trim() == response.Body.Trim();
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Status, Headers, Body);
            }
        }

        public class Session
        {
            public List<ParsedRequest> Requests { get; set; } = new();
            public AuthInfo Authenticated { get; set; } = new AuthInfo();
            public Session(JObject har)
            {
                var entries = har["log"]["entries"];
                foreach (var entry in entries.Cast<JObject>())
                {
                    ParsedRequest request = new(entry["request"] as JObject, entry["response"] as JObject);
                    request.SetTime(entry["startedDateTime"].Value<string>(), entry["time"].Value<double>());
                    Requests.Add(request);
                }
            }

            public List<string> GetHosts()
            {
                return Requests.Select(r => new Uri(r.Url).Host).Distinct().ToList();
            }

            public List<string> GetEndPointsForHost(string host)
            {
                return Requests.Where(r => new Uri(r.Url).Host == host).Select(r => new Uri(r.Url).AbsolutePath).Distinct().ToList();
            }

            public override string ToString()
            {
                return $"Session with {Requests.Count} requests, {(Authenticated.Type != AuthType.None ? Authenticated.ToString() : "No authentication used")}\n" +
                    string.Join("\n", GetHosts().Select(h => $"Endpoints for Host: {h}\n{string.Join("\n", GetEndPointsForHost(h).Select(e => $"    |  {e}"))}\n"));
            }
        }

        public class AuthInfo
        {
            public AuthType Type { get; set; } = AuthType.None;
            public JToken Credentials { get; set; } = JValue.CreateNull();

            public override string ToString()
            {
                return $"Authenticated using {Type} authentication";
            }
        }

        public enum AuthType
        {
            None,
            Basic,
            Bearer,
            Other
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

    }

}