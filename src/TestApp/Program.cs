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
            //await Test1_ApiV3ServiceDisco(repo);

            // 2. Using the V2 Feed through the V3 Client
            await Test2_ApiV2FeedAdaptor(repo);
        }

        private static async Task Test1_ApiV3ServiceDisco(NuGetRepository repo)
        {
            var desc = await repo.GetRepositoryDescription();
            Console.WriteLine("Version: " + desc.Version.ToString());
            Console.WriteLine("Mirrors: " + String.Join(", ", desc.Mirrors.Select(u => u.ToString())));
            Console.WriteLine("Services:");
            foreach (var service in desc.Services)
            {
                Console.WriteLine("* " + service.Name + " " + service.RootUrl.ToString());
            }
        }

        private static async Task Test2_ApiV2FeedAdaptor(NuGetRepository repo)
        {
            // Get the v2 client
            var packageRepo = await repo.CreateV2FeedClient();

            // List the top 10 packages
            var packages = packageRepo.Search("", Enumerable.Empty<string>(), allowPrereleaseVersions: true).Take(10).ToList();
            Console.WriteLine("Top 10 Packages on {0}:", repo.Url);
            foreach (var package in packages)
            {
                Console.WriteLine("* " + package.Id + " " + package.Version.ToString());
            }
        }
    }
}
