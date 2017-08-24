using Microsoft.Test.Apex.Hosts;

namespace NuGet.Tests.Apex
{
    public class NuGetUIProjectTestExtensionVerifier : RemoteReferenceTypeTestExtensionVerifier
    {
        /// <summary>
        /// Gets the test extension that is being verified.
        /// </summary>
        protected new NuGetUIProjectTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as NuGetUIProjectTestExtension;
            }
        }
    }
}
