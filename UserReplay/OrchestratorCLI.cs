using System.Composition.Hosting;
using CommandLine;
using Newtonsoft.Json.Linq;
using static UserReplay.Parse;

namespace UserReplay
{

    [Verb("cli", HelpText = "Run the Orchestrator with a CLI interface")]
    public partial class OrchestratorCLI : Verb
    {
        private bool Exit = false;
        public Orchestrator Orchestrator = new Orchestrator();
        public ReplayContext ReplayContext = null;
        public override async Task<bool> Run()
        {
            while (!Exit)
            {
                DisplayOptions();
                string input = GetUserInput("Enter the desired operation");
                if (string.IsNullOrEmpty(input) || !int.TryParse(input, out int selectedOption) || selectedOption < 1 || selectedOption > GetOptions().Count)
                {
                    Console.WriteLine($"Invalid operation {input}, try again");
                    continue;
                }
                switch (GetOptions().ElementAt(selectedOption - 1))
                {
                    case "Load HAR file":

                        string fileName = GetUserInput("Enter the HAR file name");
                        if (File.Exists(fileName))
                        {
                            string content = File.ReadAllText(fileName);

                            Session session = new Session(JObject.Parse(content));
                            Orchestrator = Orchestrator.FromSession(session);
                        }
                        break;
                    case "Load Flow file":
                        string flowFileName = GetUserInput("Enter the Flow file name");
                        if (File.Exists(flowFileName))
                        {
                            Orchestrator.LoadUserFlow(flowFileName);
                        }
                        break;
                    case "Save Flow file":
                        string flowFileSaveName = GetUserInput("Enter the name to save the Flow file as")  + ".json";
                        Orchestrator.SaveUserFlow(flowFileSaveName);
                        Console.WriteLine($"Saved {Orchestrator.UserFlow.Count} elements to file ");
                        break;
                    case "Start Replay":
                        ReplayContext = new ReplayContext(Orchestrator.UserFlow);
                        Console.WriteLine($"Started replay with {Orchestrator.UserFlow.Count} elements");
                        break;
                    case "Display next element":
                        Console.WriteLine($"Next element:{ReplayContext.FlowElements.Peek()}");
                        break;
                    case "Play next element":
                        var result = await ReplayContext.PlayNext();
                        Console.WriteLine($"Sent Request {result.Request.Method} to {result.Request.Url} and got response:\n{result.Value["Response"]}");
                        break;
                    case "Display previous element":
                        Console.WriteLine($"Previous element:{ReplayContext.Completed.Last()}");
                        break;
                    case "Display flow":
                        foreach (FlowElement flowElement in Orchestrator.UserFlow)
                        {
                            Console.WriteLine(flowElement.Value);
                        }
                        break;
                    case "Find relations":
                        Orchestrator.FindRelations();
                        break;
                    case "Show relation variables":
                        Console.WriteLine($"Current variables: {JToken.FromObject(ReplayContext.Exported)}");
                        break;
                    case "Exit":
                        Exit = true;
                        break;
                }
            }
            return true;
        }

        private void DisplayOptions()
        {
            Console.WriteLine("\nAvailable operations:");
            Console.WriteLine(string.Join(", ", GetOptions().Select((option, index) => $"{index + 1}: {option}")));
        }

        private List<string> GetOptions()
        {
            List<string> options = new();
            if (Orchestrator.UserFlow.Count == 0)
            {
                options.Add("Load HAR file");
                options.Add("Load Flow file");
            }
            else if (ReplayContext is null)
            {
                options = [.. options, "Find relations", "Start Replay", "Display flow", "Save Flow file"];
            }
            else
            {
                if (ReplayContext.Completed.Count != 0)
                {
                    options = [.. options, "Display previous element"];
                }
                options = [.. options, "Display next element", "Play next element", "Show relation variables", "Save Flow file"];
            }
            options.Add("Exit");
            return options;
        }

        public static string GetUserInput(string prompt)
        {
            if (Console.IsInputRedirected) throw new Exception($"Cannot prompt, StdIn is redirected");
            Console.Write($"{prompt}: ");
            string response = Console.ReadLine();
            return response;
        }

    }

}