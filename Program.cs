using System;
using System.Net.Http;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace Linky
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            app.FullName = app.Name = "linky";
            app.VersionOptionFromAssemblyAttributes(Assembly.GetExecutingAssembly());
            app.HelpOption();

            var urlArgument = app.Argument<string>("url",
                                                   "Root URL to check",
                                                   opts =>
                                                   {
                                                       opts.IsRequired(errorMessage: "URL is required.");
                                                   });

            var recursiveOption = app.Option<bool>("-r|--recursive",
                                                   "Recursively follow links on the root URL.",
                                                   CommandOptionType.NoValue);

            var verboseOption = app.Option<bool>("-v|--verbose",
                                                 "Output all links, not just those that error.",
                                                 CommandOptionType.NoValue);

            app.OnExecuteAsync(async _ =>
            {
                var rootUrl = urlArgument.ParsedValue;
                var recursive = recursiveOption.HasValue();
                var verbose = verboseOption.HasValue();

                var parser = new Parser(rootUrl, recursive, verbose, new HttpClient());

                await parser.ParseAsync(rootUrl);
            });

            return app.Execute(args);
        }
    }
}
