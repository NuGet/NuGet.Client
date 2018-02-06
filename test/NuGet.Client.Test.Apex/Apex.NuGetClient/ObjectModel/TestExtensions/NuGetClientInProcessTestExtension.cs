using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.ObjectModel.TestExtensions
{
    public class NuGetClientInProcessTestExtension<TObjectUnderTest, TVerify> : RemoteReferenceTypeTestExtension<TObjectUnderTest, TVerify>
        where TVerify : NuGetClientInProcessTestExtensionVerifier
        where TObjectUnderTest : class
    {
    }
}
