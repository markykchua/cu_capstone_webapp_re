
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
            
            int i = 0;
            foreach (ParsedRequest parsedRequest in session.Requests)
            {
                

                if (dependents.Contains(parsedRequest)){
                    SourceDestinationPair sourceDestinationPair = new SourceDestinationPair(authRequest.Response, "where in the source", parsedRequest, "where in the destination");
                    FlowElements.Add(Tuple.Create(i, sourceDestinationPair));

                } else {
                    SourceDestinationPair sourceDestinationPair = new SourceDestinationPair(null, "N/A", parsedRequest, "N/A");
                    FlowElements.Add(Tuple.Create(i, sourceDestinationPair));
                    
                }
                    
                
                i++;
            }

            //JObject parsedAuthResponse = JObject.FromObject(parsedRequest.Response); 
            //
            // for(int i = 0; i < session.Requests.Count; i++){
            //     ParsedRequest ithRequest = session.Requests[i];
            //     if (dependents.Contains(ithRequest)){
                    
            //     }
            //     FlowElements.Add(new Tuple<int, object>(i,session.Requests[i]));
            // }

            // var authRequests = session.GetAuthRequests();
            // foreach (ParsedRequest parsedRequest in authRequests)
            // {
                
            //     foreach (ParsedRequest usesAuth in session.GetAuthRequestUses(parsedRequest))
            //     {
            //         JObject parsedAuthResponse = JObject.FromObject(parsedRequest.Response);

            //         SourceDestinationPair sourceDestinationPair = new SourceDestinationPair(parsedRequest.Response, "where in the source", usesAuth, "where in the destination");
            //     }
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

    public class SourceDestinationPair{
        //one that needs to use auth request's response
        public ParsedRequest parsedRequest{get;set;}
        //auth requests response
        public ParsedResponse parsedResponse {get;set;}
        public string source {get;set;}
        public string destination {get;set;}

        public SourceDestinationPair(ParsedResponse parsedResponse, string source, ParsedRequest parsedRequest, string destination)
        {
            
            
            if (source == "where in the source")
            {
                
                JObject jObject = JObject.FromObject(parsedRequest);
                if (jObject["access_token"] != null)
                {
                    jObject["access_token"] = "{{AccessTokenHere}}";
                }
                //error here
                this.parsedRequest = jObject.ToObject<ParsedRequest>();
                
            } else {
                this.parsedRequest = parsedRequest;
            }
            
            this.parsedResponse = parsedResponse;
            this.source = source;
            this.destination = destination;

        }
        

    }
}