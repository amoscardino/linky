using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Linky
{
    [Command(Name = "linky")]
    [HelpOption]
    [VersionOptionFromMember(MemberName = "GetVersion")]
    public class ParseCommand
    {
        private readonly Parser _parser;

        [Argument(0, "URL", "URL to scan.")]
        public string StartingUrl { get; set; }

        [Option("-r|--recursive", "Recursively follow links on the root URL.", CommandOptionType.NoValue)]
        public bool Recurse { get; set; }

        [Option("-v|--verbose", "Output all links, not just those that error.", CommandOptionType.NoValue)]
        public bool Verbose { get; set; }

        public ParseCommand()
        {
            _parser = new Parser(new HttpClient());
        }

        public async Task OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrWhiteSpace(StartingUrl))
            {
                app.ShowHelp();
                return;
            }

            await _parser.ParseAsync(StartingUrl, Recurse, Verbose);
        }

        private string GetVersion()
        {
            return typeof(ParseCommand)
                .Assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
        }
    }
}