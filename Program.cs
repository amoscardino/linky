using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Linky
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            await Host
                .CreateDefaultBuilder()
                .RunCommandLineApplicationAsync<ParseCommand>(args);
        }
    }
}
