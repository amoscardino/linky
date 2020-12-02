using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Linky
{
    public class Parser
    {
        private string _rootUrl;

        private readonly HttpClient _httpClient;

        public Parser(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task ParseAsync(string startingUrl, bool recurse = false, bool verbose = false)
        {
            startingUrl = CleanUrl(startingUrl);
            _rootUrl = new Uri(startingUrl).GetLeftPart(UriPartial.Authority);

            var urls = new Dictionary<string, int>();
            urls.Add(startingUrl, 0);

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
                        response = await _httpClient.GetAsync(url);
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
                        if (verbose)
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
                    if (!recurse && url != startingUrl)
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
    }
}