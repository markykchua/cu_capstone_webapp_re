
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
using System.Linq.Expressions;
using System.ComponentModel.Design;

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
            public List<Tuple<FlowRequest, FlowResponse>> FlowElements{get;set;}
            public Tuple<FlowRequest, FlowResponse> SelectedElement{get;set;}
            public Dictionary<string, string> PlaceHolders{get;set;}

            public UserFlow(JObject har)
            {
                FlowElements = new List<Tuple<FlowRequest, FlowResponse>>();

                var entries = har["log"]["entries"];
                foreach (var entry in entries.Cast<JObject>())
                {
                    FlowRequest request = new(entry["request"] as JObject);
                    FlowResponse response = new(entry["response"] as JObject);
                    FlowElements.Add(Tuple.Create(request, response));
                }

            }

            public void moveElementUp(int ElementLocation)
            {
                if (ElementLocation > 0)
                {
                    Tuple<FlowRequest, FlowResponse> temp = FlowElements[ElementLocation];
                    FlowElements[ElementLocation] = FlowElements[ElementLocation - 1];
                    FlowElements[ElementLocation - 1] = temp;
                } else {
                    throw new Exception("Invalid location");
                }
            }

            public void moveElementDown(int ElementLocation)
            {
                if (ElementLocation < FlowElements.Count - 1)
                {
                    Tuple<FlowRequest, FlowResponse> temp = FlowElements[ElementLocation];
                    FlowElements[ElementLocation] = FlowElements[ElementLocation + 1];
                    FlowElements[ElementLocation + 1] = temp;
                } else {
                    throw new Exception("Invalid location");
                }
            }

            public void SelectElement(int ElementLocation)
            {
                if (ElementLocation < FlowElements.Count)
                {
                    SelectedElement = FlowElements[ElementLocation];
                } else {
                    throw new Exception("Invalid location");
                }
            }

            public void removeElement(int ElementLocation)
            {
                if (ElementLocation < FlowElements.Count)
                {
                    FlowElements.RemoveAt(ElementLocation);
                } else {
                    throw new Exception("Invalid location");
                }
            }


            public void playFlow(){
                //
                //
            }


        }

        public class FlowRequest
            {
                public string Url { get; set; }
                public HttpMethod Method { get; set; }
                public Dictionary<string, string> QueryParams { get; set; }
                public Dictionary<string, string> Headers { get; set; }
                public string Body { get; set; }
                public string RequestVersion { get; set; }
                public Dictionary<string, string> Dependencies { get; set; }


                public FlowRequest(JObject request)
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