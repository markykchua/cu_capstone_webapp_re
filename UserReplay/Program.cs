using CommandLine;
using Serilog;
using UserReplay;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Set to Debug or appropriate level
            .WriteTo.Console()
            .CreateLogger();

        var parser = new Parser(with =>
        {
            with.IgnoreUnknownArguments = true;
            with.HelpWriter = Console.Error;
        });

        try
        {
            var parsed = parser.ParseArguments(args,
                typeof(Replay),
                typeof(Parse)
            );

            await parsed.WithParsedAsync<Verb>(async verb =>
            {
                Log.Information($"Running {verb.GetType().Name}");
                var success = await verb.Run();
                if (!success)
                {
                    Environment.Exit(1);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}