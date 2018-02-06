using System;
using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.ObjectModel.TestExtensions
{
    /// <summary>
    /// NuGetClientInProcessTestExtensionVerifier Class
    /// </summary>
    public class NuGetClientInProcessTestExtensionVerifier : RemoteReferenceTypeTestExtensionVerifier
    {
        protected static bool CompareValues(object value, string expectedValue)
        {
            if (value != null)
            {
                return string.Equals(value.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);
            }

            return string.IsNullOrEmpty(expectedValue);
        }
    }
}
