
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UserReplay
{
    [Verb("parse", HelpText = "Parse HAR file into readable format")]
    class Parse : Verb
    {
        [Option('f', "file", Required = true, HelpText = "Path to the HAR file")] public required string FileName { get; set; }
        [Option('o', Required = false, HelpText = "Export to OpenAPI spec")] public string SwaggerFile { get; set; }
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

                    if (!string.IsNullOrEmpty(SwaggerFile))
                    {

                        Log.Information($"Writing OpenAPI spec to: {SwaggerFile}");
                        var openApiSpec = GenerateOpenApiSpec(session);
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

        private string GenerateOpenApiSpec(Session session)
        {
            var openApi = new JObject
            {
                ["openapi"] = "3.0.0",
                ["info"] = new JObject
                {
                    ["title"] = "UserReplay OpenAPI Spec",
                    ["version"] = "1.0.0"
                },
                ["servers"] = new JArray(session.GetHosts().Select(h => new JObject
                {
                    ["url"] = h
                })),
                ["paths"] = new JObject()
            };
            foreach (var request in session.Requests.Where(r => r.Response.Status >= 200 && r.Response.Status < 300))
            {
                var path = request.UrlTemplate();
                if (!(openApi["paths"] as JObject).ContainsKey(path))
                {
                    string requestContentType = ContentType(request.Headers, request.Body);
                    string responseContentType = ContentType(request.Response.Headers, request.Response.Body);
                    openApi["paths"][path] = new JObject
                    {
                        [request.Method.ToString().ToLower()] = new JObject
                        {
                            ["summary"] = $"{request.Method} to {path}",
                            ["responses"] = new JObject
                            {
                                [request.Response.Status.ToString()] = new JObject
                                {
                                    ["description"] = "Successful response",
                                    ["content"] = new JObject
                                    {
                                        [responseContentType] = new JObject
                                        {
                                            ["schema"] = GenerateSchema(request.Response.Body, responseContentType)
                                        }
                                    }
                                }
                            }
                        }
                    };
                    if (request.Method != HttpMethod.GET && !string.IsNullOrEmpty(request.Body))
                    {
                        openApi["paths"][path][request.Method.ToString().ToLower()]["requestBody"] = new JObject
                        {
                            ["content"] = new JObject
                            {
                                [requestContentType] = new JObject
                                {
                                    ["schema"] = GenerateSchema(request.Body, requestContentType)
                                }
                            }
                        };
                    }
                }
            }
            return openApi.ToString(Formatting.Indented);
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
        }

        public static string ContentType(Dictionary<string, string> headers, string body)
        {
            if (headers.TryGetValue("content-type", out string contentType))
            {
                return contentType.Split(";")[0];
            }
            else if (!string.IsNullOrEmpty(body))
            {
                if (body.TryParse(out JToken _))
                {
                    return "application/json";
                }
                else if (body.TrimStart().StartsWith("<"))
                {
                    return body.Contains("<html") ? "text/html" : "application/xml";
                }
            }
            return "text/plain";
        }


        public JObject GenerateSchema(string body, string contentType)
        {
            Log.Information($"Generating schema for {contentType}");
            if (contentType == "application/x-www-form-urlencoded")
            {
                return JObject.FromObject(ParseUrlFormEncoded(body));
            }
            else if (body.TryParse(out JToken token))
            {
                return GenerateSchema(token);
            }
            return new JObject
            {
                ["type"] = "string"
            };
        }

        private Dictionary<string, string> ParseUrlFormEncoded(string urlEncodedString)
        {
            var result = new Dictionary<string, string>();
            var pairs = urlEncodedString.Split('&');

            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = System.Web.HttpUtility.UrlDecode(keyValue[0]);
                    var value = System.Web.HttpUtility.UrlDecode(keyValue[1]);
                    result[key] = value;
                }
            }

            return result;
        }

        private JObject GenerateSchema(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    };
                    foreach (var prop in (token as JObject).Properties())
                    {
                        obj["properties"][prop.Name] = GenerateSchema(prop.Value);
                    }
                    return obj;
                case JTokenType.Array:
                    var arr = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = token.Children().Any() ? GenerateSchema((token as JArray)[0]) : new JObject()
                    };
                    return arr;
                case JTokenType.Integer:
                    return new JObject
                    {
                        ["type"] = "integer"
                    };
                case JTokenType.Float:
                    return new JObject
                    {
                        ["type"] = "number"
                    };
                case JTokenType.Boolean:
                    return new JObject
                    {
                        ["type"] = "boolean"
                    };
                case JTokenType.Null:
                    return new JObject
                    {
                        ["type"] = "null"
                    };
                case JTokenType.Date:
                    return new JObject
                    {
                        ["type"] = "string",
                        ["format"] = "date-time"
                    };
                case JTokenType.Bytes:
                    return new JObject
                    {
                        ["type"] = "string",
                        ["format"] = "byte"
                    };
                default:
                    return new JObject
                    {
                        ["type"] = "string"
                    };
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

            public List<ParsedRequest> GetAuthRequests()
            {
                return Requests.Select(r => AuthInfo.GetAuthInfo(r, this) is BearerAuth bearerAuth ? bearerAuth.AuthRequest : null).Where(a => a != null).Distinct().ToList();
            }

            public List<ParsedRequest> GetAuthRequestUses(ParsedRequest authRequest)
            {
                return Requests.Where(r => AuthInfo.GetAuthInfo(r, this) is BearerAuth bearerAuth && bearerAuth.AuthRequest == authRequest).ToList();
            }

            public override string ToString()
            {
                return $"Session with {Requests.Count} requests\n" +
                    string.Join("\n", GetHosts().Select(h => $"Endpoints for Host: {h}\n{string.Join("\n", GetEndPointsForHost(h).Select(e => $"    |  {e}"))}\n")) +
                    (GetAuthRequests().Count > 0 ? $"Auth requests: {string.Join("\n", GetAuthRequests().Select(a => $"-{new Uri(a.Url).AbsolutePath} (Used {GetAuthRequestUses(a).Count} times)"))}\n" : "");
            }
        }

        public abstract class AuthInfo
        {

            public static AuthInfo GetAuthInfo(ParsedRequest request, Session session)
            {
                if (!request.Headers.TryGetValue("authorization", out string authHeader))
                {
                    return new NoneAuth();
                }
                else if (Enum.TryParse(authHeader.Split(" ")[0], true, out AuthType authType))
                {
                    return authType switch
                    {
                        AuthType.None => new NoneAuth(),
                        AuthType.Basic => new BasicAuth(request),
                        AuthType.Bearer => new BearerAuth(request, session),
                        _ => throw new InvalidOperationException("Only Basic and Bearer authentication is supported"),
                    };
                }
                else
                {
                    return new OtherAuth(authHeader);
                }

            }
        }

        public class NoneAuth : AuthInfo
        {
            public NoneAuth() { }
            public override string ToString()
            {
                return "No authentication";
            }
        }

        public class BasicAuth : AuthInfo
        {
            public string Username { get; set; }
            public string Password { get; set; }

            public BasicAuth(ParsedRequest request)
            {
                string authHeader = request.Headers["authorization"];
                string[] split = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Split(" ")[1])).Split(":");
                Username = split[0];
                Password = split[1];
            }

            public override string ToString()
            {
                return $"Basic Auth: {Username} / {Password}";
            }
        }

        public class BearerAuth : AuthInfo
        {
            public string Token { get; set; }
            public ParsedRequest AuthRequest { get; set; }

            public BearerAuth(ParsedRequest request, Session session)
            {
                Token = request.Headers["authorization"].Split(" ")[1];
                AuthRequest = GetTokenOrigin(session);
            }

            public ParsedRequest GetTokenOrigin(Session session)
            {
                foreach (var request in session.Requests)
                {
                    if (request.Response.Body.Contains(this.Token))
                    {
                        return request;
                    }
                }
                return null;
            }

            public override string ToString()
            {
                return $"Bearer Auth: {Token}";
            }
        }

        public class OtherAuth(string authHeader) : AuthInfo
        {
            public string AuthHeader { get; set; } = authHeader;

            public override string ToString()
            {
                return $"Other Auth: {AuthHeader}";
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

}