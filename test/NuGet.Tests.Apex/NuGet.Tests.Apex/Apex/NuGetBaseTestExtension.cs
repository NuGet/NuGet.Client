using Microsoft.Test.Apex.Hosts;

namespace NuGet.Tests.Apex
{
    public abstract class NuGetBaseTestExtension<TObjectUnderTest, TVerify> :
        RemoteReferenceTypeTestExtension<TObjectUnderTest, TVerify>
        where TVerify : RemoteTestExtensionVerifier
        where TObjectUnderTest : class
    {
    }
}
