using System;

namespace NuGet.Protocol.Core.Types
{
    public class PushCommandResource : INuGetResource
    {
        private readonly string _pushEndpoint;

        public PushCommandResource(string pushEndpoint)
        {
            // _pushEndpoint may be null
            _pushEndpoint = pushEndpoint;
        }

        public string GetPushEndpoint()
        {
            return _pushEndpoint;
        }
    }
}
