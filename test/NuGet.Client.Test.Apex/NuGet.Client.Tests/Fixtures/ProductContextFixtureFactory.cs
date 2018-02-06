using System;
using System.Collections.Generic;
using NuGetClient.Test.Foundation.TestAttributes.Context;
using NuGetClient.Test.Integration.Platform;

namespace NuGetClient.Test.Integration.Fixtures
{
    /// <summary>
    /// Manages the lifetime of multiple Visual Studio host fixtures grouped by Product context
    /// </summary>
    public class ProductContextFixtureFactory : IContextFixtureFactory, IDisposable
    {
        private Dictionary<Product, VisualStudioHostFixture> fixtures;

        public ProductContextFixtureFactory()
        {
            this.fixtures = new Dictionary<Product, VisualStudioHostFixture>();
        }
        
        public VisualStudioHostFixture GetVisualStudioHostFixtureForContext(Context context)
        {
            if (this.fixtures == null)
            {
                throw new ObjectDisposedException("this");
            }

            VisualStudioHostFixture fixture;

            if (!this.fixtures.TryGetValue(context.Product, out fixture))
            {
                fixture = new VisualStudioHostFixture();
                fixture.VisualStudioHostConfiguration.TargetSku = context.TargetSkuConfiguration();
                this.fixtures.Add(context.Product, fixture);
            }

            return fixture;
            //throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (this.fixtures != null)
            {
                foreach (VisualStudioHostFixture fixture in this.fixtures.Values)
                {
                    IDisposable disposable = fixture as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }

                this.fixtures.Clear();
                this.fixtures = null;
            }
        }
    }
}
