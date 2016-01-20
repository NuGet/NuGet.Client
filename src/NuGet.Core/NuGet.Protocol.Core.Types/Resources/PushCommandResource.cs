using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    public class PushCommandResource : INuGetResource
    {
        private readonly string _pushEndpoint;

        private readonly Func<Uri, CancellationToken, Task<ICredentials>> _promptForCredentials;
        private readonly Action<Uri, ICredentials>  _credentialsSuccessfullyUsed;
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;

        public PushCommandResource(string pushEndpoint,
            Func<Uri, CancellationToken, Task<ICredentials>> promptForCredentials,
            Action<Uri, ICredentials>  credentialsSuccessfullyUsed,
            Func<Task<HttpHandlerResource>> messageHandlerFactory)
        {
            // _pushEndpoint may be null
            _pushEndpoint = pushEndpoint;

            _promptForCredentials = promptForCredentials;
            _credentialsSuccessfullyUsed = credentialsSuccessfullyUsed;
            _messageHandlerFactory = messageHandlerFactory;
        }

        public PackageUpdater GetPackageUpdater()
        {
            return new PackageUpdater(_pushEndpoint, 
                _messageHandlerFactory, 
                _promptForCredentials, 
                _credentialsSuccessfullyUsed);
        }

        public string GetPushEndpoint()
        {
            return _pushEndpoint;
        }
    }
}
