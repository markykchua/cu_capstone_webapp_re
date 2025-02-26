using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public static partial class Utils
{
    public static string GetJPath(this JObject root, JToken token)
    {
        if (token == null)
        {
            return null;
        }
        if (token == root)
        {
            return "$";
        }
        if (!root.Descendants().Contains(token))
        {
            return null;
        }
        var tokenPath = token.Path;
        var rootPath = root.Path;
        if (tokenPath.StartsWith(rootPath))
        {
            return "$" + tokenPath[rootPath.Length..];
        }
        return null;
    }

    public static JToken SelectByJPath(this JObject root, string jpath)
    {
        IEnumerable<JToken> selected = root.SelectTokens(jpath);
        if (!selected.Any())
        {
            return JValue.CreateNull();
        }
        else if (selected.Count() > 1)
        {
            return new JArray(selected);
        }
        else
        {
            return selected.First();
        }
    }

    public static JObject GenerateSchema(string body, string contentType)
    {
        if (contentType == "application/x-www-form-urlencoded")
        {
            return FromUrlFormEncoded(body);
        }
        else if (contentType == "application/xml")
        {
            XDocument xDoc = XDocument.Parse(body);
            return GenerateSchema(JObject.Parse(JsonConvert.SerializeXNode(xDoc)));
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

    private static JObject GenerateSchema(JToken token)
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

    public static JObject FromUrlFormEncoded(string urlEncodedString)
    {
        var result = new JObject();
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

    public static string ToUrlFormEncoded(JObject decoded)
    {
        var keyValuePairs = decoded.Properties()
            .Select(prop => $"{System.Web.HttpUtility.UrlEncode(prop.Name)}={System.Web.HttpUtility.UrlEncode(prop.Value.ToString())}");
        return string.Join("&", keyValuePairs);
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

    [GeneratedRegex(@"/\{(\w+)\}/", RegexOptions.Compiled)]
    private static partial Regex PathParameterRegex();
    internal static readonly string[] openApiIgnoreHeaders = ["accept", "content-type", "authorization"];

    public static string GenerateOpenApiSpec(UserFlow flow)
    {
        var openApi = new JObject
        {
            ["openapi"] = "3.0.0",
            ["info"] = new JObject
            {
                ["title"] = "UserReplay OpenAPI Spec",
                ["version"] = "1.0.0"
            },
            ["servers"] = new JArray(GetHosts(flow.FlowElements).Select(h => new JObject
            {
                ["url"] = h
            })),
            ["paths"] = new JObject()
        };
        foreach (var element in flow.FlowElements.Where(r => r.Response.Status >= 200 && r.Response.Status < 300))
        {
            var path = UrlTemplate(element);
            if (!(openApi["paths"] as JObject).ContainsKey(path))
            {
                string requestContentType = ContentType(element.Request.Headers, element.Request.Body);
                string responseContentType = ContentType(element.Response.Headers, element.Response.Body);
                openApi["paths"][path] = new JObject
                {
                    [element.Request.Method.ToString().ToLower()] = new JObject
                    {
                        ["summary"] = $"{element.Request.Method} to {path}",
                        ["responses"] = new JObject
                        {
                            [element.Response.Status.ToString()] = new JObject
                            {
                                ["description"] = "Successful response",
                                ["content"] = new JObject
                                {
                                    [responseContentType] = new JObject
                                    {
                                        ["schema"] = GenerateSchema(element.Response.Body, responseContentType)
                                    }
                                }
                            }
                        }
                    }
                };
                if (element.Request.Method != UserReplay.HttpMethod.GET && !string.IsNullOrEmpty(element.Request.Body))
                {
                    openApi["paths"][path][element.Request.Method.ToString().ToLower()]["requestBody"] = new JObject
                    {
                        ["content"] = new JObject
                        {
                            [requestContentType] = new JObject
                            {
                                ["schema"] = GenerateSchema(element.Request.Body, requestContentType)
                            }
                        }
                    };
                }
                foreach (var query in element.Request.QueryParams)
                {
                    openApi["paths"][path][element.Request.Method.ToString().ToLower()]["parameters"] = new JArray
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
                foreach (var header in element.Request.Headers.Where(h => !openApiIgnoreHeaders.Where(s => s.Equals(h.Key, StringComparison.CurrentCultureIgnoreCase)).Any()))
                {
                    openApi["paths"][path][element.Request.Method.ToString().ToLower()]["parameters"] = new JArray
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
                    openApi["paths"][path][element.Request.Method.ToString().ToLower()]["parameters"] = new JArray
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

    public static List<string> GetHosts(List<FlowElement> elements)
    {
        return elements.Select(r => new Uri(r.Request.Url).Host).Distinct().ToList();
    }

    public static List<string> GetEndPointsForHost(List<FlowElement> elements, string host)
    {
        return elements.Where(r => new Uri(r.Request.Url).Host == host).Select(r => new Uri(r.Request.Url).AbsolutePath).Distinct().ToList();
    }
    public static Regex numberIdSegment = new(@"\d+", RegexOptions.Compiled);
    public static Regex uuidSegment = new(@"[a-f0-9]{8}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{12}", RegexOptions.Compiled);
    public static Regex urlSegmentMatcher = new(@"(\w+)", RegexOptions.Compiled);

    private static string UrlTemplate(FlowElement element)
    {
        string withoutQuery = new Uri(element.Request.Url).AbsolutePath;

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

    private static string GetPreviousSegment(string path, int matchIndex)
    {
        var segments = path.Substring(0, matchIndex).Split('/');
        return segments.Length > 1 ? segments[^2] : "id";
    }
}