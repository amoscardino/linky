using System;
using System.Net.Http;
using McMaster.Extensions.CommandLineUtils;

namespace Linky
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

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

            app.OnExecuteAsync(async _ =>
            {
                var rootUrl = urlArgument.ParsedValue;
                var recursive = recursiveOption.HasValue();

                var parser = new Parser(rootUrl, recursive, new HttpClient());

                await parser.ParseAsync(rootUrl);
            });

            return app.Execute(args);
        }
    }
}
