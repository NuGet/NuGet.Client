using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.ObjectModel.TestExtensions
{
    public abstract class NuGetBaseTestExtension<TObjectUnderTest, TVerify> :
        RemoteReferenceTypeTestExtension<TObjectUnderTest, TVerify>
        where TVerify : RemoteTestExtensionVerifier
        where TObjectUnderTest : class
    {
    }
}
