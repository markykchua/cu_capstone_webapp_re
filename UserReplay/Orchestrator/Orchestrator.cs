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

        public UserFlow CurrentFlow { get; set; } = new();

        public Orchestrator(UserFlow flow)
        {
            CurrentFlow = flow;
        }

        public static Orchestrator LoadFromFile(string fileName)
        {
            string fileContents = File.ReadAllText(fileName);
            JToken token = JToken.Parse(fileContents);
            UserFlow loadedFlow = token.ToObject<UserFlow>();
            Console.WriteLine($"Loaded {loadedFlow.FlowElements.Count} elements");
            return new Orchestrator(loadedFlow);
        }

        public void SaveUserFlow(string fileName)
        {
            JObject jsonFlow = JToken.FromObject(CurrentFlow) as JObject;

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
                relation.InsertRelation(CurrentFlow);
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