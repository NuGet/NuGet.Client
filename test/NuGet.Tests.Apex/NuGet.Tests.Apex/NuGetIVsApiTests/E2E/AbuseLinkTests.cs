using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Tests.Apex.Platform;
using NuGet.Tests.Foundation.TestAttributes;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex.NuGetIVsApiTests.E2E
{
    public class AbuseLinkTests : SharedVisualStudioHostTestClass
    {
        public AbuseLinkTests(VisualStudioHostFixtureFactory productContextFixtureFactory) : base(productContextFixtureFactory)
        { }

        [Test]
        [Platform(PlatformIdentifier.Wpf, PlatformVersion.v_4_6_1)]
        [Product(Product.Blend)]
        public void AbuseLink()
        {
            ContextImplementation contexImp = this.CurrentContext.GetImplementation();
            this.EnsureVisualStudioHostForContext();

            ProjectTestExtension project = contexImp.CreateProject(this.VisualStudio);
        }
    }
}
