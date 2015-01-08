using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [Serializable]
    public class NuGetProtocolException : Exception
    {
        public NuGetProtocolException() { }
        public NuGetProtocolException(string message) : base(message) { }
        public NuGetProtocolException(string message, Exception inner) : base(message, inner) { }
        protected NuGetProtocolException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
