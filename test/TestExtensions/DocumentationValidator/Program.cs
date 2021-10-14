using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Common;

namespace DocumentationValidator
{
    class Program
    {
        static readonly string LogCodeTemplate = "https://docs.microsoft.com/en-us/nuget/reference/errors-and-warnings/{0}";

        static async Task<int> Main(string[] args)
        {
            IList<NuGetLogCode> allLogCodes = GetNuGetLogCodes();
            List<NuGetLogCode> undocumentedLogCodes = new();
            HttpClient httpClient = new();

            Console.WriteLine($"Checking {allLogCodes.Count} log codes for documentation.");

            for (int i = 0; i < allLogCodes.Count; i++)
            {
                if (i % 20 == 0 && i != 0) // The check will take some time, so display progress updates.
                {
                    Console.WriteLine($"Checked {i} of {allLogCodes.Count}");
                }

                if (!await IsLogCodeDocumentedAsync(httpClient, allLogCodes[i]))
                {
                    undocumentedLogCodes.Add(allLogCodes[i]);
                }
            }
            Console.WriteLine("Completed checking log codes for documentation.");

            Console.WriteLine($"Undocumented Log Codes Count {undocumentedLogCodes.Count}");

            foreach (var logCode in undocumentedLogCodes)
            {
                Console.Error.WriteLine(logCode);
            }

            return undocumentedLogCodes.Any() ? 1 : 0;
        }

        private static async Task<bool> IsLogCodeDocumentedAsync(HttpClient httpClient, NuGetLogCode logCode)
        {
            var result = await httpClient.GetAsync(string.Format(LogCodeTemplate, logCode));
            return result.IsSuccessStatusCode;
        }

        private static IList<NuGetLogCode> GetNuGetLogCodes()
        {
            var list = GetEnumList<NuGetLogCode>(); ;
            list.Remove(NuGetLogCode.Undefined);
            return list;

            IList<T> GetEnumList<T>()
            {
                T[] array = (T[])Enum.GetValues(typeof(T));
                List<T> list = new List<T>(array);
                return list;
            }
        }
    }
}
