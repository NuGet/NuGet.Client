using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Configuration
{
    public class NuGetConfigurationException : Exception
    {
        public NuGetConfigurationException(string message)
            : base(message)
        {
        }

        public NuGetConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
