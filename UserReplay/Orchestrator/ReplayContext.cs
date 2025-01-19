using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace UserReplay
{

    public class ReplayContext
    {
        public Queue<FlowElement> FlowElements = new();
        public List<FlowElement> Completed = new();
        public Dictionary<string, JToken> Exported = new();
        public ReplayContext(List<FlowElement> elements)
        {
            FlowElements = new Queue<FlowElement>(elements);
        }

        public async Task<FlowElement> PlayNext()
        {
            if (!FlowElements.Any())
            {
                throw new InvalidOperationException("No remaining elements in flow");
            }
            FlowElement current = FlowElements.Dequeue();
            current.FillRequestPlaceholders(Exported);
            IFlurlResponse response = await current.Request.Replay();
            current.UpdateResponse(new ParsedResponse(response));
            foreach (KeyValuePair<string, JToken> export in current.GetExported())
            {
                Console.WriteLine($"Exported variable \"{export.Key}\": {export.Value}");
                Exported[export.Key] = export.Value;
            }
            return current;
        }
    }
}