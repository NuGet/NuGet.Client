using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGetClient.Test.Integration.Apex;
using NuGetClient.Test.Integration.Fixtures;
using NuGetClient.Test.Integration.Platform;
using NuGetClient.Test.Foundation.TestAttributes;
using NuGetClient.Test.Foundation.TestAttributes.Context;


namespace NuGetClient.Test.Integration.Tests.E2E
{
    public class AbuseLinkTests: IntegrationNuGetTestClass
    {
        public AbuseLinkTests(ProductContextFixtureFactory productContextFixtureFactory) : base(productContextFixtureFactory)
        { }

        [Test]
        [Platform(PlatformIdentifier.Wpf, PlatformVersion.v_4_5_2)]
        //[Platform(PlatformIdentifier.UWP, PlatformVersion.UnspecifiedVersion)]
        //[Product(Product.VS)]
        [Product(Product.Blend)]
        public void AbuseLink()
        {
            ContextImplementation contextImpl = this.CurrentContext.GetImplementation();
            this.EnsureVisualStudioHostForContext();

            ProjectTestExtension projectExtension = contextImpl.CreateProject(this.VisualStudio);

            //Open Package Manage UI
            //contextImpl.OpenPackageManager();

            var nugetTestService = GetNuGetTestService();
            var uiWindow = nugetTestService.GetUIWindowfromProject(projectExtension);
        }
       
    }
}
