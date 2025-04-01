using System.Composition;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public class CookieRelation : IFlowRelation
{
    public static string TokenVariableName = "extracted_cookie_token_";
    public void InsertRelation(UserFlow flow)
    {
        Dictionary<FlowElement, List<FlowElement>> cookiePairings = new();

        // Dictionary to store response bodies with "Set-Cookie" header
        Dictionary<string, FlowElement> responseBodies = flow.FlowElements
            .Where(r => r.Response.Headers.ContainsKey("Set-Cookie"))
            .DistinctBy(r => r.Response.Headers["Set-Cookie"])
            .ToDictionary(e => e.Response.Headers["Set-Cookie"], e => e);

        int cookieCount = 0;

        // Iterate over FlowElements with "cookie" header in the request
        foreach (FlowElement flowElement in flow.FlowElements.Where(e => e.Request.Cookies.ContainsKey("csrftoken") || e.Request.Cookies.ContainsKey("sessionid")))
        {
            //Console.WriteLine($"Found cookie in request to {flowElement.Request.Url}");
            var cookies = flowElement.Request.Cookies["csrftoken"] ?? flowElement.Request.Cookies["sessionid"];
            var cookieRequests = responseBodies.Where(r => r.Key.Contains(cookies)).Select(r => r.Value);

            foreach (var cookietype in flowElement.Request.Cookies)
            {
                Console.WriteLine($"Found cookie in request to {flowElement.Request.Url} with {cookietype.Key} and {cookietype.Value}");

                if (!flow.ExternalVariables.ContainsKey(cookietype.Key))
                {
                    flow.ExternalVariables[cookietype.Key] = JToken.FromObject(cookietype.Value);
                    cookieCount++;
                }


            }


            if(flow.ExternalVariables.ContainsKey(TokenVariableName))
            {
                flow.ExternalVariables[TokenVariableName] = JToken.FromObject(cookies);
                cookieCount++;
            }
            
        }


        Console.WriteLine($"Found {cookieCount} cookies in the flow");
    }
}