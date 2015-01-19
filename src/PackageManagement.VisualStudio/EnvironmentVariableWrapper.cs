using System;
using System.Security;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class EnvironmentVariableWrapper : IEnvironmentVariableReader
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
