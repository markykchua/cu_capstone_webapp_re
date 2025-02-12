using Serilog;
using static UserReplay.Parse;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using System.CodeDom.Compiler;
using Flurl.Http;
//using UserReplay.Orchestrator;



namespace Tests;
public class Tests
{


    public class OrchestratorTests{
        private Session session;
        //private Orchestrator orchestrator;

        [SetUp]
        public void SetUp(){
            var har = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            session = new(har);
        }

        [Test]
        public void orchTest1(){


            Assert.Pass();

            
        }
    }

    public class sessionTests{
        private Session session;
        [SetUp]
        public void Setup()
        {

            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            session = new(har);
        }

        [Test]
        public void Session_GetHosts_Test()
        {
            List<String> getHostsList = new List<String> {"www.googletagmanager.com", "cdn.optimizely.com", "location-services-prd.tesla.com"};
            Assert.AreEqual(session.GetHosts(),getHostsList);
        }

        [Test]
        public void Session_GetEndPointsForHost_Test()
        {
            List<String> getHostsList = new List<String> {"/gtm.js"};
            Assert.AreEqual(session.GetEndPointsForHost("www.googletagmanager.com"),getHostsList);
        }

        [Test]
        public void Session_GetAuthRequests_Test(){
            var har2 = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            Session session2 = new(har2);

            List<UserReplay.FlowElement> pr = session2.GetAuthRequests();

            Assert.IsNotEmpty(pr.ElementAt(0).ToString());
        }

        [Test]
        public void Session_GetAuthRequestUses_Test()
        {
            var har2 = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            Session session2 = new(har2);

            List<UserReplay.FlowElement> pr = session2.GetAuthRequests();
            List<UserReplay.FlowElement> RequestsUsingAuth = session2.GetAuthRequestUses(pr.ElementAt(0));

            TestContext.WriteLine(session2.Requests.Count);
            TestContext.WriteLine(RequestsUsingAuth.Count);

            Assert.IsNotEmpty(RequestsUsingAuth.ElementAt(0).ToString());
        }


    }

    public class ParsedRequestTests{
        private Session session;
        public List<UserReplay.FlowElement> Requests;

        [SetUp]
        public void Setup()
        {

            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            session = new(har);
            Requests = session.Requests;

        }

        [Test]
        public void ParsedRequest_UrlTemplate_Test(){
            UserReplay.FlowElement parsedRequest = Requests[0];
            Assert.AreEqual("/gtm.js",parsedRequest.UrlTemplate());
        }

        [Test]
        public async Task ParsedRequest_ReplayAsync(){
            Assert.Pass();
            List<UserReplay.FlowElement> bearerAuthRequests = session.GetAuthRequests();
                    foreach (UserReplay.FlowElement request in session.Requests)
                    {
                        IFlurlResponse response = await request.Replay();
                        if (bearerAuthRequests.Contains(request))
                        {
                            string token = JToken.Parse(await response.ResponseMessage.Content.ReadAsStringAsync())["access_token"].ToString();
                            foreach (UserReplay.FlowElement authUse in session.GetAuthRequestUses(request))
                            {
                                authUse.Headers["Authorization"] = $"Bearer {token}";
                            }
                        }
                        UserReplay.ParsedResponse parsedResponse = new(response);
                        Log.Information(request.ToString());
                        Log.Information(parsedResponse.ToString());
                        if (parsedResponse != request.Response)
                        {
                            if (parsedResponse.Status >= 400)
                            {
                                Log.Error($"Request failed unexpectedly: {parsedResponse.Status}");
                                Assert.Fail();
                            }
                            else
                            {
                                Log.Warning($"Request was successful but response did not match recorded response");
                            }
                        }
                    }
                    Assert.Pass();
        }



    }


    public class ParsedResponseTests{
        private Session session;
        public UserReplay.ParsedResponse parsedResponse;

        [SetUp]
        public void Setup()
        {
            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            session = new(har);
            parsedResponse = session.Requests[0].Response;

        }

        [Test]
        public void ParsedResponse_Equals(){
            // parsed response changes based on the received response

            // var har2 = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            // Session session2 = new(har2);
            // ParsedResponse parsedResponse2 = session2.Requests[0].Response;

            // Assert.True(parsedResponse.Equals(parsedResponse2));
        }

        [Test]
        public void ParsedResponse_GetHashCode(){
            //hashcode changes based on response

            // int hashCode = parsedResponse.GetHashCode();
            // Assert.AreEqual(-1563660955,hashCode);
        }
    }
    public class AuthInfoTests{
        //GetAuthInfo
    }

    public class NoneAuthTests{
        //nothing
    }

    public class BasicAuthTests{
        // Only Constructor, get and set
    }
    public class BearerAuthTests{
        private Session session;
        public List<UserReplay.FlowElement> Requests;

        [SetUp]
        public void Setup(){
            var har = JObject.Parse(File.ReadAllText("www.reddit.com.har"));
            session = new(har);
            Requests = session.Requests;
        }

        [Test]
        public void BearerAuth_GetTokenOrigin_Test(){
            // If the setup passes it is able to run GetTokenOrigin without error
            Assert.Pass();
        }
    }

    public class OtherAuthTests{
        // Only Constructor, get and set
    }

    public class JTokenExtensionsTests{
        // TryParse
    }
    
}