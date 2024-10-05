
using CommandLine;
using Serilog.Events;

namespace UserReplay
{
    public abstract class Verb
    {
        [Option('l', "LoggingLevel", HelpText = "Logging Level(Verbose, Debug, Information, Warning, Error, Fatal)", Default = LogEventLevel.Information)] public LogEventLevel LoggingLevel { get; set; }
        public abstract Task<bool> Run();

    }
}