using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "push", "PushCommandDescription;DefaultConfigDescription",
        MinArgs = 1, MaxArgs = 2, UsageDescriptionResourceName = "PushCommandUsageDescription",
        UsageSummaryResourceName = "PushCommandUsageSummary", UsageExampleResourceName = "PushCommandUsageExamples")]
    public class PushCommand : Command
    {
        [Option(typeof(NuGetCommand), "PushCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "CommandApiKey")]
        public string ApiKey { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandTimeoutDescription")]
        public int Timeout { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandDisableBufferingDescription")]
        public bool DisableBuffering { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandNoSymbolsDescription")]
        public bool NoSymbols { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            string packagePath = Arguments[0];
            string sourcePath = Source;
            string apiKeyValue = ApiKey;
            int timeoutSeconds = Timeout;

            if (string.IsNullOrEmpty(apiKeyValue) && Arguments.Count > 1)
            {
                apiKeyValue = Arguments[1];
            }

            try
            {
                await PushRunner.Run(
                    Settings,
                    SourceProvider,
                    packagePath,
                    Source,
                    apiKeyValue,
                    Timeout,
                    DisableBuffering,
                    NoSymbols,
                    Console);
            }
            catch (TaskCanceledException ex)
            {
                string timeoutMessage = LocalizedResourceManager.GetString(nameof(NuGetResources.PushCommandTimeoutError));
                throw new AggregateException(ex, new Exception(timeoutMessage));
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.InnerException is WebException)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }
    }
}
