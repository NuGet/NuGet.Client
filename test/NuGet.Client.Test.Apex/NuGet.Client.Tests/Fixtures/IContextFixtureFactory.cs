using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Integration.Fixtures
{
    /// <summary>
    /// Build a Visual Studio host fixture based on properties of a provided Context
    /// </summary>
    public interface IContextFixtureFactory
    {
        VisualStudioHostFixture GetVisualStudioHostFixtureForContext(Context context);
    }
}
