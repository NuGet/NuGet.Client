using Microsoft.Framework.Logging;
using System.Threading.Tasks;

namespace NuGet.Strawman.Commands
{
    public class RestoreCommand
    {
        private ILogger _log;

        public RestoreCommand(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<RestoreCommand>();
        }

        public Task<RestoreResult> ExecuteAsync(RestoreRequest request)
        {
            _log.LogInformation($"Restoring packages for '{request.Project.FilePath}'");

            return Task.FromResult(new RestoreResult());
        }   
    }
}
