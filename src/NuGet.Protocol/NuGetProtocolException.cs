using System;

namespace NuGet.Protocol
{
    public class NuGetProtocolException : Exception
    {
        public NuGetProtocolException(string message)
            : base(message)
        {

        }

    }
}
