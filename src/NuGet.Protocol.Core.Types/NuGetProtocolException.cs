using System;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Base protocol exception type containing a message and optional inner exception.
    /// </summary>
    public class NuGetProtocolException : Exception
    {
        public NuGetProtocolException(string message)
            : base(message)
        {

        }

        public NuGetProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
