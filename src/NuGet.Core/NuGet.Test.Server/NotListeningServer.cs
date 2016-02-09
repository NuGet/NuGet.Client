using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Test.Server
{
    public class NotListeningServer : ITestServer
    {
        public async Task<T> ExecuteAsync<T>(Func<string, Task<T>> action)
        {
            if (Mode != TestServerMode.ConnectFailure)
            {
                throw new InvalidOperationException($"The mode {Mode} is not supported by this server.");
            }

            var portReserver = new PortReserver();
            return await portReserver.ExecuteAsync(
                async (port, token) =>
                {
                    var address = $"http://localhost:{port}/";
                    return await action(address);
                },
                CancellationToken.None);
        }

        public TestServerMode Mode { get; set; } = TestServerMode.ConnectFailure;
    }
}
