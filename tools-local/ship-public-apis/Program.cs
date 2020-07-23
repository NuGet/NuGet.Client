using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace ship_public_apis
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var nugetSlnDirectory = FindNuGetSlnDirectory();
            var pathArgument = nugetSlnDirectory == null
                ? new Argument<DirectoryInfo>("path")
                : new Argument<DirectoryInfo>("path", getDefaultValue: () => nugetSlnDirectory);

            var rootCommand = new RootCommand()
            {
                pathArgument,
                new Option<bool>("--resort")
            };

            rootCommand.Description = "Copy and merge contents of PublicAPI.Unshipped.txt to PublicAPI.Shipped.txt. See https://github.com/NuGet/Home/issues/9632 for more details.";

            rootCommand.Handler = CommandHandler.Create<DirectoryInfo, bool>(MainAsync);

            return await rootCommand.InvokeAsync(args);
        }

        private static DirectoryInfo FindNuGetSlnDirectory()
        {
            var directory = Environment.CurrentDirectory;

            while (true)
            {
                if (File.Exists(Path.Combine(directory, "NuGet.sln")))
                {
                    return new DirectoryInfo(directory);
                }

                var parent = Path.GetDirectoryName(directory);
                if (string.IsNullOrEmpty(parent) || parent == directory)
                {
                    return null;
                }

                directory = parent;
            }
        }

        static async Task MainAsync(DirectoryInfo path, bool resort)
        {
            if (path == null)
            {
                Console.Error.WriteLine("No path provided");
                return;
            }

            if (!path.Exists)
            {
                Console.Error.WriteLine($"Path '{path.FullName}' does not exist");
            }

            foreach (var unshippedTxtPath in path.EnumerateFiles("PublicAPI.Unshipped.txt", new EnumerationOptions() { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true}))
            {
                if (unshippedTxtPath.Length == 0 && !resort)
                {
                    Console.WriteLine(unshippedTxtPath.FullName + ": Up to date");
                    continue;
                }

                var shippedTxtPath = Path.Combine(unshippedTxtPath.DirectoryName, "PublicAPI.Shipped.txt");
                if (!File.Exists(shippedTxtPath))
                {
                    throw new FileNotFoundException($"Cannot migrate APIs from {unshippedTxtPath.FullName}. {shippedTxtPath} not found.");
                }

                var lines = new List<string>();
                int unshippedApiCount = 0;
                using (var stream = unshippedTxtPath.OpenText())
                {
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lines.Add(line);
                            unshippedApiCount++;
                        }
                    }
                }

                using (var stream = File.OpenText(shippedTxtPath))
                {
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lines.Add(line);
                        }
                    }
                }

                lines.Sort(StringComparer.Ordinal);

                await File.WriteAllLinesAsync(shippedTxtPath, lines);
                await File.WriteAllBytesAsync(unshippedTxtPath.FullName, Array.Empty<byte>());

                Console.WriteLine($"{unshippedTxtPath.FullName}: Shipped {unshippedApiCount} APIs.");
            }
        }
    }
}
