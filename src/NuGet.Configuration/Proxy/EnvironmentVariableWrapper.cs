using System;
using System.Security;

namespace NuGet.Configuration
{
    public class EnvironmentVariableWrapper : IEnvironmentVariableReader
    {
        public string GetEnvironmentVariable(string variable)
        {
            try
            {
                return Environment.GetEnvironmentVariable(variable);
            }
            catch (SecurityException)
            {
                return null;
            }
        }
    }
}
