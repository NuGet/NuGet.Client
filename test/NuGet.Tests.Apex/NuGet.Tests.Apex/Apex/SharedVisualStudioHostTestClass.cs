using System;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio;
using Xunit;

namespace NuGet.Tests.Apex
{
    [CollectionDefinition("SharedVSHost")]
    public sealed class SharedVisualStudioHostTestCollectionDefinition : ICollectionFixture<VisualStudioHostFixtureFactory>
    {
        private SharedVisualStudioHostTestCollectionDefinition()
        {
            throw new InvalidOperationException("SharedVisualStudioHostTestCollectionDefinition only exists for metadata, it should never be constructed.");
        }
    }

    [Collection("SharedVSHost")]
    public abstract class SharedVisualStudioHostTestClass : ApexBaseTestClass
    {
        private readonly IVisualStudioHostFixtureFactory _contextFixtureFactory;
        private readonly Lazy<VisualStudioHostFixture> _hostFixture;

        protected SharedVisualStudioHostTestClass(IVisualStudioHostFixtureFactory contextFixtureFactory)
        {
            _contextFixtureFactory = contextFixtureFactory;

            _hostFixture = new Lazy<VisualStudioHostFixture>(() =>
            {
                return _contextFixtureFactory.GetVisualStudioHostFixture();
            });
        }

        public override VisualStudioHost VisualStudio
        {
            get { return _hostFixture.Value.VisualStudio; }
        }

        public override TService GetApexService<TService>()
        {
            return _hostFixture.Value.Operations.Get<TService>();
        }

        public override void EnsureVisualStudioHost()
        {
            _hostFixture.Value.EnsureHost();
        }

        public override void CloseVisualStudioHost()
        {
            VisualStudio.Stop();
        }

        public IOperations Operations
        {
            get
            {
                return _hostFixture.Value.Operations;
            }
        }
    }
}
