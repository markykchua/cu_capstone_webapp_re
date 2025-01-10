
using CommandLine;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Serilog;
using Flurl;
using System.Net;
using static UserReplay.Parse;
using System.Reflection.Emit;
using Flurl.Http.Content;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UserReplay
{
    public class Orchestrator
    {
        public UserFlow userFlow{get;set;}

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
    

    public class UserFlow
    {
        public List<Tuple<int, SourceDestinationPair>> FlowElements{get;set;}
        public List<Tuple<int, int>> ListOfOperations{get;set;}

        public UserFlow(Session session)
        {
            FlowElements = new List<Tuple<int, SourceDestinationPair>>();
            var authRequest = session.GetAuthRequests()[0];
            var dependents = session.GetAuthRequestUses(authRequest);

            List<Tuple<ParsedRequest,ParsedRequest>> dependencyDependentPair = new List<Tuple<ParsedRequest,ParsedRequest>>();
            
            //int i = 0;
            // foreach (ParsedRequest parsedRequest in session.Requests)
            // {
                

            //     if (dependents.Contains(parsedRequest)){
            //         SourceDestinationPair sourceDestinationPair = new SourceDestinationPair(authRequest.Response, "where in the source", parsedRequest, "where in the destination");
            //         FlowElements.Add(Tuple.Create(i, sourceDestinationPair));

            //     } else {
            //         SourceDestinationPair sourceDestinationPair = new SourceDestinationPair(null, "N/A", parsedRequest, "N/A");
            //         FlowElements.Add(Tuple.Create(i, sourceDestinationPair));
                    
            //     }
                    
                
            //     i++;
            // }
            
        }

        public void assignNewLocation(int currentLoc, int newLoc)
        {
            if (currentLoc < FlowElements.Count && newLoc < FlowElements.Count)
            {
                Tuple<int, SourceDestinationPair> temp = FlowElements[currentLoc];
                FlowElements[currentLoc] = FlowElements[newLoc];
                FlowElements[newLoc] = temp;
                ListOfOperations.Add(Tuple.Create(currentLoc, newLoc));
            } else {
                throw new Exception("Invalid location");
            }
        }

        public void undoPreviousOperation()
        {
            if (ListOfOperations.Count > 0)
            {
                Tuple<int, int> lastOperation = ListOfOperations[ListOfOperations.Count - 1];
                assignNewLocation(lastOperation.Item2, lastOperation.Item1);
                ListOfOperations.RemoveAt(ListOfOperations.Count - 1); 
                ListOfOperations.RemoveAt(ListOfOperations.Count - 1); // remove the last two operations from the list
            } else {
                throw new Exception("No operations to undo");
            }
        }

        public void resetOperations()
        {
            ListOfOperations.Clear();
            FlowElements.OrderBy(x => x.Item1);
        }

        public void playFlow(){
            //List<ParsedRequest> bearerAuthRequests = session.GetAuthRequests();
                    // foreach (ParsedRequest request in session.Requests)
                    // {
                    //     IFlurlResponse response = await request.Replay();
                    //     if (bearerAuthRequests.Contains(request))
                    //     {
                    //         string token = JToken.Parse(await response.ResponseMessage.Content.ReadAsStringAsync())["access_token"].ToString();
                    //         foreach (ParsedRequest authUse in session.GetAuthRequestUses(request))
                    //         {
                    //             authUse.Headers["Authorization"] = $"Bearer {token}";
                    //         }
                    //     }
                    //     ParsedResponse parsedResponse = new(response);
                    //     Log.Information(request.ToString());
                    //     Log.Information(parsedResponse.ToString());
                    //     if (parsedResponse != request.Response)
                    //     {
                    //         if (parsedResponse.Status >= 400)
                    //         {
                    //             Log.Error($"Request failed unexpectedly: {parsedResponse.Status}");
                    //             return false;
                    //         }
                    //         else
                    //         {
                    //             Log.Warning($"Request was successful but response did not match recorded response");
                    //         }
                    //     }
                    // }
            //
        }


        


    }

    public class FlowRequests
        {
            public string Url { get; set; }
            public HttpMethod Method { get; set; }
            public Dictionary<string, string> QueryParams { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string Body { get; set; }
            public string RequestVersion { get; set; }
            public Dictionary<string, string> Dependencies { get; set; }


            public FlowRequests(JObject request, JObject response)
            {
                Url = request["url"].Value<string>();
                Method = Enum.Parse<HttpMethod>(request["method"].Value<string>());
                Headers = request.ContainsKey("headers") ? (request["headers"] as JArray).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
                QueryParams = request.ContainsKey("queryString") ? (request["queryString"] as JArray).DistinctBy(h => h["name"]).ToDictionary(q => q["name"].Value<string>(), q => q["value"].Value<string>()) : new Dictionary<string, string>();
                Body = request.ContainsKey("postData") ? request["postData"]["text"].Value<string>() : "";
                RequestVersion = request["httpVersion"].Value<string>();
            }

            internal static readonly string[] supportedHttpVersions = ["1.0", "1.1", "2.0", "3.0"];


        public override string ToString()
            {
                return $"REQUEST {Method} {Url} - {JObject.FromObject(QueryParams)}{(Method != HttpMethod.GET && !string.IsNullOrEmpty(Body) ? $"\nBody: {Body}" : "")} \n==>\n";
            }
    }

    public class FlowResponse
        {
            public int Status { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string Body { get; set; }
            //Dictionary of the type of dependencies it requests for
            public Dictionary<string, string> HasDependents { get; set; }

            public FlowResponse(JObject response)
            {
                Status = response["status"].Value<int>();
                Headers = response.ContainsKey("headers") ? (response["headers"] as JArray).DistinctBy(h => h["name"]).ToDictionary(h => h["name"].Value<string>(), h => h["value"].Value<string>()) : new Dictionary<string, string>();
                Body = response["content"]?["text"]?.Value<string>() ?? "";
            }
            public FlowResponse(IFlurlResponse response)
            {
                Status = response.StatusCode;
                Headers = response.Headers.DistinctBy(h => h.Name).ToDictionary(h => h.Name, h => h.Value);
                Body = response.ResponseMessage.Content.ReadAsStringAsync().Result;
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

    public class SourceDestinationPair{
        //one that needs to use auth request's response
        public ParsedRequest parsedRequest{get;set;}
        //auth requests response
        public ParsedResponse parsedResponse {get;set;}
        public string source {get;set;}
        public string destination {get;set;}

        public SourceDestinationPair(ParsedResponse parsedResponse, string source, ParsedRequest parsedRequest, string destination)
        {
            this.parsedRequest = parsedRequest;
            this.parsedResponse = parsedResponse;
            this.source = source;
            this.destination = destination;

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