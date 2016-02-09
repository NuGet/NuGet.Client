using System;
using System.Threading.Tasks;

namespace NuGet.Test.Server
{
    public enum TestServerMode
    {
        ConnectFailure,
        ServerProtocolViolation
    }

    public interface ITestServer
    {
        Task<T> ExecuteAsync<T>(Func<string, Task<T>> action);

        TestServerMode Mode { get; set; }
    }
}