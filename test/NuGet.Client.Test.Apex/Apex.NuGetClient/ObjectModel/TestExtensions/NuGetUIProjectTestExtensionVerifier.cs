using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.ObjectModel.TestExtensions
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
