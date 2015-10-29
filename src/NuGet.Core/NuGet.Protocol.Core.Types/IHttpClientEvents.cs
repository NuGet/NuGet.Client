using System;

namespace NuGet.Protocol.Core.Types
{
    public interface IHttpClientEvents : IProgressProvider
    {
        event EventHandler<WebRequestEventArgs> SendingRequest;
    }
}