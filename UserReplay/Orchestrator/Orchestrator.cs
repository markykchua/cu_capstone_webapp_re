using System.Composition.Hosting;
using System.Reflection;
using static UserReplay.Parse;
using System.Text.Json;
using System.IO;
using Newtonsoft.Json.Linq;

namespace UserReplay
{
    public class Orchestrator
    {

        public List<FlowElement> UserFlow { get; set; } = new();

        public static Orchestrator FromSession(Session session)
        {
            return new Orchestrator()
            {
                UserFlow = [.. session.Requests.Select(r => new FlowElement(r))]
            };
        }

        public void LoadUserFlow(string fileName)
        {
            //string fileContents = File.ReadAllText(fileName);
            //UserFlow = JsonSerializer.Deserialize<List<FlowElement>>(fileContents);

            string fileContents = File.ReadAllText(fileName);
            JToken token = JToken.Parse(fileContents);
            List<ParsedRequest> requestsToLoad = token.ToObject<List<ParsedRequest>>();
            System.Console.WriteLine($"Loaded {requestsToLoad.Count} elements");

            UserFlow = requestsToLoad.Select(r => new FlowElement(r)).ToList();
        }

        public void SaveUserFlow(string fileName)
        {
            List<ParsedRequest> requestsToSave = UserFlow.Select(r => r.Request).ToList();

            string flowContents = JsonSerializer.Serialize(requestsToSave);
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
                relation.InsertRelation(UserFlow);
            }
        }



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