using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Client.Diagnostics;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && String.Equals(args[0], "dbg", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            AsyncMain(args).Wait();
        }

        private static async Task AsyncMain(string[] args)
        {
            string url = "https://api.nuget.org";
            if (args.Length > 0)
            {
                url = args[0];
            }

            // Setting up the client
            var repo = new NuGetRepository(new Uri(url), new ColoredConsoleTraceSink());

            // 1. Connecting and getting the repository description
            var desc = await repo.GetRepositoryDescription();
            Console.WriteLine("Version: " + desc.Version.ToString());
            Console.WriteLine("Mirrors: " + String.Join(", ", desc.Mirrors.Select(u => u.ToString())));
            Console.WriteLine("Services:");
            foreach (var service in desc.Services)
            {
                Console.WriteLine("* " + service.Name + " " + service.RootUrl.ToString());
            }
        }
    }
}
