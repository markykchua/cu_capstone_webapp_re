
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
                            // skip headers that are not allowed with Flurl
                            if (header["name"].Value<string>().StartsWith(":") || header["name"].Value<string>().Equals("content-length", StringComparison.CurrentCultureIgnoreCase))
                            {
                                continue;
                            }
                            else
                            {
                                string headerName = header["name"].Value<string>();
                                string headerValue = header["value"].Value<string>();
                                request.WithHeader(headerName, headerValue);
                            }
                        }
                        foreach (var query in entry["request"]["queryString"])
                        {
                            string queryName = query["name"].Value<string>();
                            string queryValue = query["value"].Value<string>();
                            request.SetQueryParam(queryName, queryValue);
                        }
                        Log.Information($"Sending {entry["request"]["method"]} request to {entry["request"]["url"]} - expecting {entry["response"]["status"]} response");
                        //return true;
                        var response = entry["request"]["method"].Value<string>() switch
                        {
                            "GET" => await request.GetAsync(),
                            "POST" => await request.PostStringAsync(entry["request"]["postData"]["text"].ToString()),
                            "PUT" => await request.PutStringAsync(entry["request"]["postData"]["text"].ToString()),
                            "PATCH" => await request.PatchStringAsync(entry["request"]["postData"]["text"].ToString()),
                            "OPTIONS" => await request.OptionsAsync(),
                            "DELETE" => await request.DeleteAsync(),
                            _ => throw new InvalidOperationException()
                        };
                        if (response.StatusCode >= 400)
                        {
                            Log.Error($"Endpoint responded {response.StatusCode} (original status: {entry["response"]["status"]})");
                            return false;
                        }
                        else
                        {
                            Log.Information($"Endpoint responded {response.StatusCode} {response.ResponseMessage} (original status: {entry["response"]["status"]})");
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