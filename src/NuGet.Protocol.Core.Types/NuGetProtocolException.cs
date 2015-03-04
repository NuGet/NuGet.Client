using System;

namespace NuGet.Protocol.Core.Types
{
    public class NuGetProtocolException : Exception
    {
        public NuGetProtocolException(string message)
            : base(message)
        {

        }

    }
}
