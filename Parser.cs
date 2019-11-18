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
        private HttpClient _http;
        private string _rootUrl;
        private bool _recurse;

        public Parser(string rootUrl, bool recurse, HttpClient http)
        {
            _rootUrl = new Uri(rootUrl).GetLeftPart(UriPartial.Authority);
            _recurse = recurse;
            _http = http;
        }

        public async Task ParseAsync(string startingUrl)
        {
            var urls = new Dictionary<string, int>();
            urls.Add(startingUrl, 0);

            while (urls.Any(x => x.Value == 0))
            {
                var urlsToProcess = urls.Where(x => x.Value == 0).Select(x => x.Key).ToList();

                foreach (var url in urlsToProcess)
                {
                    Console.Write(url);

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

                    Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine($" [{(int)response.StatusCode}]");
                    Console.ResetColor();

                    if (!response.IsSuccessStatusCode)
                        continue;

                    if (urls.Count == 1 || _recurse)
                    {
                        if (!IsInternalUrl(url))
                            continue;

                        if (!response.Content.Headers.ContentType.MediaType.StartsWith("text/html"))
                            continue;

                        try
                        {
                            var html = await response.Content.ReadAsStringAsync();

                            GetUrlsFromHtml(html).ForEach(x => urls.TryAdd(x, 0));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\tUnable to parse HTML.");
                        }
                    }
                }
            }
        }

        private List<string> GetUrlsFromHtml(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

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
                .Where(link => !string.IsNullOrWhiteSpace(link.Value))
                .Select(link => CleanUrl(link.Value))
                .Where(url => url.StartsWith("http"))
                .ToList();
        }

        private string CleanUrl(string url)
        {
            url = (url ?? string.Empty).Trim();

            if (url.StartsWith("//"))
                url = $"https:{url}";

            if (url.StartsWith("/"))
                url = $"{_rootUrl}{url}";

            url = url.TrimEnd('/');

            return url;
        }

        private bool IsInternalUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) && url.StartsWith(_rootUrl);
        }
    }
}