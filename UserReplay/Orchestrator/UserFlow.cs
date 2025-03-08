using System.Reflection;
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

    public static UserFlow LoadFromFile(string fileName)
    {
        string fileContents = File.ReadAllText(fileName);
        JToken token = JToken.Parse(fileContents);
        UserFlow loadedFlow = token.ToObject<UserFlow>();
        Console.WriteLine($"Loaded {loadedFlow.FlowElements.Count} elements");
        return loadedFlow;
    }

    public void SaveUserFlow(string fileName)
    {
        JObject jsonFlow = JToken.FromObject(this) as JObject;

        string flowContents = jsonFlow.ToString();
        File.WriteAllText(fileName, flowContents);
    }

    public void FindRelations()
    {
        var relationTypes = Assembly.GetExecutingAssembly()
                                    .GetTypes()
                                    .Where(t => typeof(IFlowRelation).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                                    .ToList();

        Console.WriteLine($"Trying to find {relationTypes.Count()} kinds of relations");
        foreach (var relationType in relationTypes)
        {
            var relation = (IFlowRelation)Activator.CreateInstance(relationType);
            Console.WriteLine($"Inserting {relation.GetType().Name}");
            relation.InsertRelation(this);
        }

        Console.WriteLine($"Found {ExternalVariables.Count} external variables");
    }

}