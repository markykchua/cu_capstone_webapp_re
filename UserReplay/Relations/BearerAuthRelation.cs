using System.Composition;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UserReplay;

public class BearerAuthRelation : IFlowRelation
{
    public static string TokenVariableName = "extracted_auth_token_";
    public void InsertRelation(UserFlow flow)
    {
        Dictionary<FlowElement, List<FlowElement>> authPairings = new();

        Dictionary<string, FlowElement> responseBodies = flow.FlowElements.DistinctBy(r => r.Response.Body).ToDictionary(e => e.Response.Body, e => e);

        foreach (FlowElement flowElement in flow.FlowElements.Where(e => e.Request.Headers.ContainsKey("Authorization") && e.Request.Headers["Authorization"].StartsWith("Bearer ")))
        {
            var token = flowElement.Request.Headers["Authorization"].Split("Bearer ")[1];
            var authRequests = responseBodies.Where(r => r.Key.Contains(token)).Select(r => r.Value);
            if (authRequests.Any())
            {
                var authRequest = authRequests.First();
                if (!authPairings.ContainsKey(authRequest))
                {
                    authPairings[authRequest] = new();
                }
                authPairings[authRequest].Add(flowElement);
            }
        }
        int authCount = 0;
        foreach (var pair in authPairings)
        {
            authCount++;
            Console.WriteLine($"Found auth request ({pair.Key.Request.Url}) with {pair.Value.Count} uses");
            pair.Key.AddExport($"extracted_auth_token_{authCount}", new Export("$.Response.Body.access_token"));
            foreach (FlowElement element in pair.Value)
            {
                element.Request.Headers["Authorization"] = $"Bearer {{{{extracted_auth_token_{authCount}}}}}";
            }
        }
    }
}