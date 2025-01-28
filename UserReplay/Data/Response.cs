using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace UserReplay
{


    public class ParsedResponse
    {
        public int Status { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Cookies { get; set; }

        public ParsedResponse(JObject response)
        {
            Status = response["status"].Value<int>();
            Headers = response.ContainsKey("headers") ? (response["headers"] as JArray).DistinctBy(h => h["name"]).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
            Body = response["content"]?["text"]?.Value<string>() ?? "";
            Cookies = (response["cookies"] as JArray).ToDictionary(c => c["name"].Value<string>(), c => c["value"].Value<string>());
        }
        public ParsedResponse(IFlurlResponse response)
        {
            Status = response.StatusCode;
            Headers = response.Headers.DistinctBy(h => h.Name).ToDictionary(h => h.Name, h => h.Value);
            Body = response.ResponseMessage.Content.ReadAsStringAsync().Result;
            Cookies = response.Cookies.ToDictionary(c => c.Name, c => c.Value);
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

}