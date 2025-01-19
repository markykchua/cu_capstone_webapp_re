using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UserReplay
{
    public static class Utils
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
            Log.Information($"Generating schema for {contentType}");
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
    }
}