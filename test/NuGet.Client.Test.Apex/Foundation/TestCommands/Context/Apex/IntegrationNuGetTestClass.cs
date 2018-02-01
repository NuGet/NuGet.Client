using System;
using Foundation;
using Xunit;
using NuGet.Client.Tests.Fixtures;
using Microsoft.Test.Apex.VisualStudio;
using Foundation.TestAttributes.Context;

namespace NuGet.Client.Tests.Apex
{
    /// <summary>
    /// Define the test collection for NuGet test and the associated fixture
    /// </summary>
    [CollectionDefinition("NuGet.Client.Tests")]
    public sealed class IntegretionNuGetTestCollectionDefinition : ICollectionFixture<ProductContextFixtureFactory>
    {
        private IntegretionNuGetTestCollectionDefinition()
        {
            throw new InvalidOperationException("IntegrationNuGetCollectionDefinition only exists for metadata, it should never be constructed");
        }
    }

    /// <summary>
    /// Base clase for a normal integration test. Runs in a Test Collection
    /// that reuse the product context for performance during betched runs.
    /// </summary>
    [Collection("NuGet.Client.Tests")]
    public abstract class IntegrationNuGetTestClass : ApexBaseTestClass
    {
        private readonly IContextFixtureFactory contextFixtureFactory;
        private readonly Lazy<VisualStudioHostFixture> hostFixture;


        protected IntegrationNuGetTestClass(IContextFixtureFactory contextFixtureFactory)
        {
            this.contextFixtureFactory = contextFixtureFactory;
            this.hostFixture = new Lazy<VisualStudioHostFixture>(() =>
            {
                if (this.CurrentContext == null)
                {
                    throw new InvalidOperationException("Attempted to access hostFixture before Context was set. Integration tests require a context to build a Visual Studio host fixture.");
                }

                VisualStudioHostFixture hostFixture = this.contextFixtureFactory.GetVisualStudioHostFixtureForContext(this.CurrentContext);
                return hostFixture;
            });
        }
        public override void EnsureVisualStudioHostForContext()
        {
            this.hostFixture.Value.EnsureHost();
        }

        public override VisualStudioHost VisualStudio
        {
            get { return this.hostFixture.Value.VisualStudio; }
        }

        public override void SetHostEnvironment(string name, string value)
        {
            this.hostFixture.Value.SetHostEnvironment(name, value);
        }
        
        public override string GetHostEnvironment(string name)
        {
            return this.hostFixture.Value.GetHostEnvironment(name);
        }

        public override TService GetApexService<TService>()
        {
            return this.hostFixture.Value.Operations.Get<TService>();
        }
    }
}
