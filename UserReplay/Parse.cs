using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UserReplay
{
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
                    string requestContentType = Utils.ContentType(request.Headers, request.Body);
                    string responseContentType = Utils.ContentType(request.Response.Headers, request.Response.Body);
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
                                            ["schema"] = Utils.GenerateSchema(request.Response.Body, responseContentType)
                                        }
                                    }
                                }
                            }
                        }
                    };
                    if (request.Method != UserReplay.HttpMethod.GET && !string.IsNullOrEmpty(request.Body))
                    {
                        openApi["paths"][path][request.Method.ToString().ToLower()]["requestBody"] = new JObject
                        {
                            ["content"] = new JObject
                            {
                                [requestContentType] = new JObject
                                {
                                    ["schema"] = Utils.GenerateSchema(request.Body, requestContentType)
                                }
                            }
                        };
                    }
                    foreach (var query in request.QueryParams)
                    {
                        openApi["paths"][path][request.Method.ToString().ToLower()]["parameters"] = new JArray
                        {
                            new JObject
                            {
                                ["name"] = query.Key,
                                ["in"] = "query",
                                ["required"] = true,
                                ["schema"] = new JObject
                                {
                                    ["type"] = "string"
                                }
                            }
                        };
                    }
                    foreach (var header in request.Headers.Where(h => !(openApiIgnoreHeaders).Where(s => s.Equals(h.Key, StringComparison.CurrentCultureIgnoreCase)).Any()))
                    {
                        openApi["paths"][path][request.Method.ToString().ToLower()]["parameters"] = new JArray
                        {
                            new JObject
                            {
                                ["name"] = header.Key,
                                ["in"] = "header",
                                ["required"] = true,
                                ["schema"] = new JObject
                                {
                                    ["type"] = "string"
                                }
                            }
                        };
                    }
                    foreach (var pathParam in PathParameterRegex().Matches(path).Select(m => m.Groups[1].Value))
                    {
                        openApi["paths"][path][request.Method.ToString().ToLower()]["parameters"] = new JArray
                        {
                            new JObject
                            {
                                ["name"] = pathParam,
                                ["in"] = "path",
                                ["required"] = true,
                                ["schema"] = new JObject
                                {
                                    ["type"] = "string"
                                }
                            }
                        };
                    }
                }
            }
            return openApi.ToString(Formatting.Indented);
        }

        public class Session
        {
            public List<FlowElement> Requests { get; set; } = new();
            public Session(JObject har)
            {
                var entries = har["log"]["entries"];
                foreach (var entry in entries.Cast<JObject>())
                {
                    FlowElement request = new(entry["request"] as JObject, entry["response"] as JObject);
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

            public List<FlowElement> GetAuthRequests()
            {
                return Requests.Select(r => AuthInfo.GetAuthInfo(r, this) is BearerAuth bearerAuth ? bearerAuth.AuthRequest : null).Where(a => a != null).Distinct().ToList();
            }

            public List<FlowElement> GetAuthRequestUses(FlowElement authRequest)
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

            public static AuthInfo GetAuthInfo(FlowElement request, Session session)
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

            public BasicAuth(FlowElement request)
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
            public FlowElement AuthRequest { get; set; }

            public BearerAuth(FlowElement request, Session session)
            {
                Token = request.Headers["authorization"].Split(" ")[1];
                AuthRequest = GetTokenOrigin(session);
            }

            public FlowElement GetTokenOrigin(Session session)
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