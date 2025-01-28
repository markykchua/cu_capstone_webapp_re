using System.Composition.Hosting;
using System.Reflection;
using static UserReplay.Parse;

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
            string fileContents = File.ReadAllText(fileName);

            // need to determine file contents / the storage format
        }

        public void SaveUserFlow(string fileName)
        {
            string path = "";

            string flowContents = "";
            // need to determine file contents / the storage format

            File.WriteAllText(path + fileName, flowContents);
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