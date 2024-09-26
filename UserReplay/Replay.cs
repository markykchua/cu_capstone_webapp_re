
using CommandLine;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Serilog;
using Flurl;

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
                    foreach (var entry in har["log"]["entries"])
                    {
                        IFlurlRequest request = new FlurlRequest(entry["request"]["url"].Value<string>()).AllowAnyHttpStatus();
                        foreach (var header in entry["request"]["headers"])
                        {
                            if (header["name"].Value<string>().StartsWith(":"))
                            {
                                continue;
                            }
                        }
                        foreach (var query in entry["request"]["queryString"])
                        {
                            request.SetQueryParam(query["name"].Value<string>(), query["value"].Value<string>());
                        }
                        Log.Information($"Sending {entry["request"]["method"]} request to {entry["request"]["url"]} - {entry["request"]["postData"]["text"]}\n{entry["request"]["headers"]}");
                        var response = entry["request"]["method"].Value<string>() switch
                        {
                            "GET" => await request.GetAsync(),
                            "POST" => await request.PostJsonAsync(new StringContent(entry["request"]["postData"]["text"].ToString())),
                            "PUT" => await request.PutJsonAsync(new StringContent(entry["request"]["postData"]["text"].ToString())),
                            "PATCH" => await request.PatchJsonAsync(new StringContent(entry["request"]["postData"]["text"].ToString())),
                            "OPTIONS" => await request.OptionsAsync(),
                            "DELETE" => await request.DeleteAsync(),
                            _ => throw new InvalidOperationException()
                        };
                        if (response.StatusCode >= 400)
                        {
                            Log.Error($"Request: {entry["request"]["url"]} Response: {response.StatusCode} (original status: {entry["response"]["status"]})");
                            return false;
                        }
                        else
                        {
                            Log.Information($"Request: {entry["request"]["url"]} Response: {response.StatusCode} {response.ResponseMessage} (original status: {entry["response"]["status"]})");
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