using Newtonsoft.Json.Linq;

namespace UserReplay;

public class UserFlow
{

    public List<FlowElement> FlowElements = new();
    public Dictionary<string, JToken> ExternalVariables = new();

    public static UserFlow FromHar(JObject har)
    {
        UserFlow parsed = new UserFlow();
        var entries = har["log"]["entries"];
        foreach (var entry in entries.Cast<JObject>())
        {
            ParsedRequest request = new(entry["request"] as JObject);
            request.SetTime(entry["startedDateTime"].Value<string>(), entry["time"].Value<double>());
            ParsedResponse response = new(entry["response"] as JObject);
            parsed.FlowElements.Add(new FlowElement(request, response));
        }
        return parsed;
    }


}