using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HtmlAgilityPack;
using McMaster.Extensions.CommandLineUtils;

namespace Linky
{
    [Command(Name = "linky")]
    [HelpOption]
    [VersionOptionFromMember(MemberName = "GetVersion")]
    public class ParseCommand
    {
        private HttpClient _http;
        private string _rootUrl;

        [Argument(0, "URL", "URL to scan.")]
        public string StartingUrl { get; set; }

        [Option("-r|--recursive", "Recursively follow links on the root URL.", CommandOptionType.NoValue)]
        public bool Recurse { get; set; }

        [Option("-v|--verbose", "Output all links, not just those that error.", CommandOptionType.NoValue)]
        public bool Verbose { get; set; }

        public ParseCommand()
        {
            // TODO: Figure out how to get DI working for this
            _http = new HttpClient();
        }

        public async Task OnExecuteAsync(CommandLineApplication app)
        {
            StartingUrl = CleanUrl(StartingUrl);

            if (string.IsNullOrWhiteSpace(StartingUrl))
            {
                app.ShowHelp();
                return;
            }

            _rootUrl = new Uri(StartingUrl).GetLeftPart(UriPartial.Authority);

            var urls = new Dictionary<string, int>();
            urls.Add(StartingUrl, 0);

            while (urls.Any(x => x.Value == 0))
            {
                // Grab any URLs from the dictionary that we have not checked. If we are recursing,
                // new URLs will be added with a status code of 0 and we will pick them up on the
                // next pass.
                var urlsToProcess = urls.Where(x => x.Value == 0).Select(x => x.Key).ToList();

                foreach (var url in urlsToProcess)
                {
                    var displayUrl = url.Length > (Console.BufferWidth - 10)
                        ? $"{url.Substring(0, Console.BufferWidth - 10)}..."
                        : url;
                    Console.Write(displayUrl);

                    var response = (HttpResponseMessage)null;

                    try
                    {
                        response = await _http.GetAsync(url);
                    }
                    catch (HttpRequestException ex)
                    {
                        urls[url] = -1;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($" [{ex.Message}]");
                        Console.ResetColor();
                        continue;
                    }

                    urls[url] = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        if (Verbose)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($" [{(int)response.StatusCode}]");
                            Console.ResetColor();
                        }
                        else
                        {
                            // Clear the current line
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write(new string(' ', Console.BufferWidth - 1));
                            Console.SetCursorPosition(0, Console.CursorTop);
                        }
                    }
                    else
                    {
                        // Write the error code and exit the loop early
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($" [{(int)response.StatusCode}]");
                        Console.ResetColor();
                        continue;
                    }

                    // Exit early if we are not recursing unless we are checking the starting URL
                    if (!Recurse && url != StartingUrl)
                        continue;

                    // Exit early if the URL is external or if the content is not HTML
                    if (!IsInternalUrl(url) || !response.Content.Headers.ContentType.MediaType.StartsWith("text/html"))
                        continue;

                    try
                    {
                        // If we made it this far, we will parse the HTML for links
                        var html = await response.Content.ReadAsStringAsync();

                        // We add each link to the dictionary with a status code of 0. Since
                        // we are using a dictionary, URLs that are already checked or slated
                        // to be checked will be ignored.
                        GetUrlsFromHtml(html).ForEach(x => urls.TryAdd(x, 0));
                    }
                    catch
                    {
                        Console.WriteLine(url);
                        Console.WriteLine($"\tUnable to parse HTML.");
                    }
                }
            }
        }

        private List<string> GetUrlsFromHtml(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Handle redirects done through a meta tag by returning that URL to be parsed next.
            var metaRefresh = htmlDoc.DocumentNode.SelectSingleNode("//meta[@http-equiv=\"refresh\"]");

            if (metaRefresh != null)
            {
                var content = metaRefresh.Attributes["content"]?.Value ?? string.Empty;

                if (!content.StartsWith("0; url="))
                    return new List<string>();

                var url = content.Replace("0; url=", string.Empty);

                return new List<string> { CleanUrl(url) };
            }

            var nodes = htmlDoc.DocumentNode.SelectNodes("//a[@href]");

            if (nodes == null || !nodes.Any())
                return new List<string>();

            return nodes
                .Cast<HtmlNode>()
                .Select(link => link.Attributes["href"])
                .Cast<HtmlAttribute>()
                .Select(link => CleanUrl(link.Value))
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();
        }

        private string CleanUrl(string url)
        {
            url = (url ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("#"))
                return string.Empty;

            if (url.StartsWith("//"))
                url = $"https:{url}";

            if (url.StartsWith("/"))
                url = $"{_rootUrl}{url}";

            if (!url.StartsWith("http"))
                url = $"https://{url}";

            url = url.TrimEnd('/');

            return url;
        }

        private bool IsInternalUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) && url.StartsWith(_rootUrl);
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