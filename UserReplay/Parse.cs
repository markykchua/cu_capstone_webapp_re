
using CommandLine;
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
                    Session session = new();
                    var entries = har["log"]["entries"];
                    var requests = new List<ParsedRequest>();
                    foreach (var entry in entries)
                    {
                        JObject parameters = new();
                        foreach (var query in entry["request"]["queryString"] ?? Enumerable.Empty<JToken>())
                        {
                            parameters.Add(query["name"].Value<string>(), query["value"].Value<string>());
                        }
                        foreach (var param in entry["request"]["postData"]?["params"] ?? Enumerable.Empty<JToken>())
                        {
                            parameters.Add(param["name"].Value<string>(), param["value"].Value<string>());
                        }
                        requests.Add(new ParsedRequest
                        {
                            Url = entry["request"]["url"].ToString(),
                            Method = entry["request"]["method"].ToString(),
                            ContentType = entry["request"]["postData"]?["mimeType"]?.ToString() ?? "",
                            Headers = new JObject(entry["request"]["headers"].Select(h => new JProperty(h["name"].Value<string>().ToLower(), h["value"])).Concat(entry["request"]["postData"]?["params"]?.Select(p => new JProperty(p["name"].Value<string>(), p["value"].Value<string>())) ?? Enumerable.Empty<JProperty>())),
                            Params = parameters,
                            Body = entry["request"]["postData"]?["text"]?.ToString() ?? "",
                            Response = new ParsedResponse
                            {
                                Status = entry["response"]["status"].ToString(),
                                ContentType = entry["response"]["content"]["mimeType"].ToString(),
                                Headers = new JObject(entry["response"]["headers"].Select(h => new JProperty(h["name"].Value<string>().ToLower(), h["value"]))),
                                Body = entry["response"]["content"]?["text"]?.ToString() ?? ""
                            }
                        });
                    }
                    foreach (var request in requests)
                    {
                        session.Requests.Add(request);
                        Log.Information(request.ToString());
                    }

                    var authHeader = requests.FirstOrDefault(r => r.Headers.ContainsKey("authorization"));
                    AuthType authType = authHeader switch
                    {
                        { } when authHeader.Headers["authorization"].ToString().StartsWith("Basic") => AuthType.Basic,
                        { } when authHeader.Headers["authorization"].ToString().StartsWith("Bearer") => AuthType.Bearer,
                        { } when authHeader.Headers.ContainsKey("authorization") => AuthType.Other,
                        _ => AuthType.None
                    };

                    session.Authenticated = new AuthInfo
                    {
                        Type = authType,
                        Credentials = authHeader?.Headers["authorization"] ?? JValue.CreateNull()
                    };

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
            public string Method { get; set; }
            public string ContentType { get; set; }
            public JObject Headers { get; set; }
            public string Body { get; set; }
            public JObject Params { get; set; }
            public ParsedResponse Response { get; set; }

            public override string ToString()
            {
                return $"{Method} {Url} - {Params}{(Method != "GET" && !string.IsNullOrEmpty(Body) ? $"\nBody: {Body}" : "")} \n==>\n{Response}\n";
            }
        }


        public class ParsedResponse
        {
            public string Status { get; set; }
            public string ContentType { get; set; }
            public JObject Headers { get; set; }
            public string Body { get; set; }

            public override string ToString()
            {
                return $"RESPONSE {Status} : ({ContentType}){(!string.IsNullOrEmpty(Body) ? $"\nBody: {(Body.Length > 200 ? Body[..200] + "......" : Body)}" : "")}\n";
            }
        }

        public class Session
        {
            public List<ParsedRequest> Requests { get; set; } = new();
            public AuthInfo Authenticated { get; set; } = new AuthInfo();
            public override string ToString()
            {
                return $"Session with {Requests.Count} requests, {(Authenticated.Type != AuthType.None ? Authenticated.ToString() : "No authentication")}";
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

    }

}