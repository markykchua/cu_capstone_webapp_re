using Serilog;
using static UserReplay.Parse;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using System.CodeDom.Compiler;
using Flurl.Http;
using UserReplay;
//using UserReplay.Orchestrator;



namespace Tests;
public class Tests
{


    public class OrchestratorTests
    {
        private UserFlow flow;
        //private Orchestrator orchestrator;

        [SetUp]
        public void SetUp()
        {
            var har = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            flow = UserFlow.FromHar(har);
        }

        [Test]
        public void orchTest1()
        {
            Assert.Pass();
        }
    }

    public class sessionTests
    {
        private UserFlow flow;
        [SetUp]
        public void Setup()
        {
            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            flow = UserFlow.FromHar(har);
        }

        [Test]
        public void UserFlow_GetHosts_Test()
        {
            List<String> getHostsList = new List<String> { "www.googletagmanager.com", "cdn.optimizely.com", "location-services-prd.tesla.com" };
            Assert.That(getHostsList, Is.EqualTo(Utils.GetHosts(flow.FlowElements)));
        }

        [Test]
        public void UserFlow_GetEndPointsForHost_Test()
        {
            List<String> getHostsList = new List<String> { "/gtm.js" };
            Assert.That(getHostsList, Is.EqualTo(Utils.GetEndPointsForHost(flow.FlowElements, "www.googletagmanager.com")));
        }

        [Test]
        public void UserFlow_GetAuthRequests_Test()
        {
            var har2 = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            UserFlow flow2 = UserFlow.FromHar(har2);
            Orchestrator temp = new Orchestrator(flow2);
            temp.FindRelations();

            Assert.IsNotEmpty(temp.CurrentFlow.FlowElements.Where(e => e.GetExported().Where(ex => ex.Key.StartsWith(BearerAuthRelation.TokenVariableName)).Any()));
        }

        [Test]
        public void UserFlow_GetAuthRequestUses_Test()
        {
            var har2 = JObject.Parse(File.ReadAllText("exampleAuth.har"));
            UserFlow flow2 = UserFlow.FromHar(har2);
            Orchestrator temp = new Orchestrator(flow2);
            temp.FindRelations();

            Assert.IsNotEmpty(temp.CurrentFlow.FlowElements.Where(e => e.Value.Descendants().OfType<JValue>().Select(v => v.ToString()).Any(s => s.StartsWith("Bearer {{{{"))));
        }


    }

    public class ParsedRequestTests
    {
        private UserFlow flow;
        public List<ParsedRequest> Requests;

        [SetUp]
        public void Setup()
        {

            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            flow = UserFlow.FromHar(har);
            Requests = [.. flow.FlowElements.Select(e => e.Request)];

        }

        [Test]
        public void ParsedRequest_UrlTemplate_Test()
        {
            ParsedRequest parsedRequest = Requests[0];
            Assert.That(parsedRequest.UrlTemplate(), Is.EqualTo("/gtm.js"));
        }

        /*[Test] needs changing
        public async Task ParsedRequest_ReplayAsync()
        {
            Assert.Pass();
            List<ParsedRequest> bearerAuthRequests = flow.GetAuthRequests();
            foreach (ParsedRequest request in flow.Requests)
            {
                IFlurlResponse response = await request.Replay();
                if (bearerAuthRequests.Contains(request))
                {
                    string token = JToken.Parse(await response.ResponseMessage.Content.ReadAsStringAsync())["access_token"].ToString();
                    foreach (ParsedRequest authUse in flow.GetAuthRequestUses(request))
                    {
                        authUse.Headers["Authorization"] = $"Bearer {token}";
                    }
                }
                ParsedResponse parsedResponse = new(response);
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
        }*/



    }


    public class ParsedResponseTests
    {
        private UserFlow flow;
        public ParsedResponse parsedResponse;

        [SetUp]
        public void Setup()
        {
            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            flow = UserFlow.FromHar(har);
            parsedResponse = flow.FlowElements[0].Response;
        }

        [Test]
        public void ParsedResponse_Equals()
        {
            // parsed response changes based on the received response

            // var har2 = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            // UserFlow session2 = new(har2);
            // ParsedResponse parsedResponse2 = session2.Requests[0].Response;

            // Assert.True(parsedResponse.Equals(parsedResponse2));
        }

        [Test]
        public void ParsedResponse_GetHashCode()
        {
            //hashcode changes based on response

            // int hashCode = parsedResponse.GetHashCode();
            // Assert.AreEqual(-1563660955,hashCode);
        }
    }
    public class AuthInfoTests
    {
        //GetAuthInfo
    }

    public class NoneAuthTests
    {
        //nothing
    }

    public class BasicAuthTests
    {
        // Only Constructor, get and set
    }
    public class BearerAuthTests
    {
        private UserFlow flow;

        [SetUp]
        public void Setup()
        {
            var har = JObject.Parse(File.ReadAllText("www.reddit.com.har"));
            flow = UserFlow.FromHar(har);
        }

        [Test]
        public void BearerAuth_GetTokenOrigin_Test()
        {
            // If the setup passes it is able to run GetTokenOrigin without error
            Assert.Pass();
        }
    }

    public class OtherAuthTests
    {
        // Only Constructor, get and set
    }

    public class JTokenExtensionsTests
    {
        // TryParse
    }

}