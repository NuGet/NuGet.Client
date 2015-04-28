using Microsoft.Framework.Logging;

namespace NuGet.Commands
{
    public class NuGetLoggerAdapter : NuGet.Client.ILogger
    {
        private ILogger _logger;

        public NuGetLoggerAdapter(ILogger logger)
        {
            _logger = logger;
        }

        public void WriteError(string message)
        {
            _logger.LogError(message);
        }

        public void WriteInformation(string message)
        {
            _logger.LogInformation(message);
        }

        public void WriteQuiet(string message)
        {
            _logger.LogDebug(message);
        }

        public void WriteVerbose(string message)
        {
            _logger.LogVerbose(message);
        }
    }
}