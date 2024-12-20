
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

        public UserFlow(Session session)
        {
            var authRequests = session.GetAuthRequests();

            List<Tuple<ParsedRequest,ParsedRequest>> dependencyDependentPair = new List<Tuple<ParsedRequest,ParsedRequest>>();
            
            foreach(ParsedRequest authPR in authRequests)
            {
                foreach(ParsedRequest usesAuthPR in session.GetAuthRequestUses(authPR))
                {
                    dependencyDependentPair.Add(Tuple.Create(authPR, usesAuthPR));
                }      
            }

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
            
        }


        


    }

    public class SourceDestinationPair{
        //one that needs to use auth request's response
        public ParsedRequest parsedRequest{get;set;}
        //auth requests response
        public ParsedResponse parsedResponse {get;set;}

        public SourceDestinationPair(ParsedResponse parsedResponse, string source, ParsedRequest parsedRequest, string destination)
        {
            this.parsedRequest = parsedRequest;
            this.parsedResponse = parsedResponse;

        }
        

    }
}