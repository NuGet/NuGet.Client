using System;

namespace NuGet.Protocol.Core.Types
{
    public class WebRequestEventArgs : EventArgs
    {
        public WebRequestEventArgs(Uri requestUri, string method)
        {
            if (requestUri == null)
            {
                throw new ArgumentNullException(nameof(requestUri));
            }
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            
            RequestUri = requestUri;
            Method = method;
        }

        public string Method { get; private set; }

        public Uri RequestUri { get; private set; }
    }
}