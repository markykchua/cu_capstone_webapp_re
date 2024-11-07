using Serilog;
using static UserReplay.Parse;
using Newtonsoft.Json.Linq;
using System.CodeDom.Compiler;
using UserReplay;
using Flurl.Http;
using CommandLine;

namespace Tests;
public class Tests
{

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
            var har2 = JObject.Parse(File.ReadAllText("www.reddit.com.har"));
            Session session2 = new(har2);

            // List<ParsedRequest> pr = session2.GetAuthRequestUses().GetRange(0,0);
            // Assert.AreEqual(pr,session.GetAuthRequests());
        }

        [Test]
        public void Session_GetAuthRequestUses_Test()
        {
            //List<ParsedRequest> getHostsList = new List<ParsedRequest> ();
            //Assert.AreEqual(session.GetAuthRequestUses(),getHostsList);
        }


    }

    public class ParsedRequestTests{
        private Session session;
        public List<ParsedRequest> Requests;

        [SetUp]
        public void Setup()
        {

            var har = JObject.Parse(File.ReadAllText("www.tesla.com.har"));
            session = new(har);
            Requests = session.Requests;

        }

        [Test]
        public void ParsedRequest_UrlTemplate_Test(){
            ParsedRequest parsedRequest = Requests[0];
            Assert.AreEqual("/gtm.js",parsedRequest.UrlTemplate());
        }

        [Test]
        public async Task ParsedRequest_ReplayAsync(){
            Assert.Pass();
            List<ParsedRequest> bearerAuthRequests = session.GetAuthRequests();
                    foreach (ParsedRequest request in session.Requests)
                    {
                        IFlurlResponse response = await request.Replay();
                        if (bearerAuthRequests.Contains(request))
                        {
                            string token = JToken.Parse(await response.ResponseMessage.Content.ReadAsStringAsync())["access_token"].ToString();
                            foreach (ParsedRequest authUse in session.GetAuthRequestUses(request))
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
        }



    }


    public class ParsedResponseTests{
        private Session session;
        public ParsedResponse parsedResponse;

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
        public List<ParsedRequest> Requests;

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