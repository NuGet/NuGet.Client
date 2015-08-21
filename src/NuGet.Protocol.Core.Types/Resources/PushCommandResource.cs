using System;

namespace NuGet.Protocol.Core.Types
{
    public class PushCommandResource : INuGetResource
    {
        private readonly string _pushEndpoint;

        public PushCommandResource(string pushEndpoint)
        {
            if (pushEndpoint == null)
            {
                throw new ArgumentNullException(nameof(pushEndpoint));
            }

            _pushEndpoint = pushEndpoint;
        }

        public string GetPushEndpoint()
        {
            return _pushEndpoint;
        }
    }
}
