
using CommandLine;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Serilog;
using Flurl;
using System.Net;
using static UserReplay.Parse;

namespace UserReplay
{

    [Verb("replay", HelpText = "Replay requests from a HAR file")]
    class Replay : Verb
    {
        [Option('f', "file", Required = true, HelpText = "Path to the HAR file")] public string FileName { get; set; }
        public override async Task<bool> Run()
        {
            try
            {
                if (!File.Exists(FileName))
                {
                    Log.Error("File does not exist");
                    return false;
                }
                else
                {
                    var har = JObject.Parse(File.ReadAllText(FileName));
                    // naive implementation
                    Session session = new(har);
                    foreach (ParsedRequest request in session.Requests)
                    {
                        IFlurlResponse response = await request.Replay();
                        ParsedResponse parsedResponse = new(response);
                        Log.Information(request.ToString());
                        Log.Information(parsedResponse.ToString());
                        if (parsedResponse != request.Response)
                        {
                            if (parsedResponse.Status >= 400)
                            {
                                Log.Error($"Request failed unexpectedly: {parsedResponse.Status}");
                                return false;
                            }
                            else
                            {
                                Log.Warning($"Request was successful but response did not match recorded response");
                            }
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred");
                return false;
            }
        }
    }
}