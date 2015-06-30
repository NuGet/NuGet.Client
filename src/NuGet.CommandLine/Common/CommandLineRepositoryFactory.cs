
using System.Windows;

namespace NuGet.Common
{
    public class CommandLineRepositoryFactory : PackageRepositoryFactory
    {
        public static readonly string UserAgent = "NuGet Command Line";

        private readonly IConsole _console;

        public CommandLineRepositoryFactory(IConsole console)
        {
            _console = console;
        }

        public override IPackageRepository CreateRepository(string packageSource)
        {
            var repository = base.CreateRepository(packageSource);
            var httpClientEvents = repository as IHttpClientEvents;
            if (httpClientEvents != null)
            {
                httpClientEvents.SendingRequest += (sender, args) =>
                {
                    if (sender != httpClientEvents)
                    {
                        return;
                    }

                    if (_console.Verbosity == Verbosity.Detailed)
                    {
                        _console.WriteLine(
                            System.ConsoleColor.Green,
                            "{0} {1}", args.Request.Method, args.Request.RequestUri);
                    }
                    string userAgent = HttpUtility.CreateUserAgentString(CommandLineConstants.UserAgent);
                    HttpUtility.SetUserAgent(args.Request, userAgent);
                };
            }

            return repository;
        }
    }
}