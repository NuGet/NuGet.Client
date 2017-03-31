using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Apex
{
    public class VisualStudioHostFixtureFactory : IVisualStudioHostFixtureFactory, IDisposable
    {
        private VisualStudioHostFixture _visualStudioHostFxiture;

        public VisualStudioHostFixture GetVisualStudioHostFixture()
        {
            if (_visualStudioHostFxiture == null)
            {
                _visualStudioHostFxiture = new VisualStudioHostFixture();
            }

            return _visualStudioHostFxiture;
        }

        public void Dispose()
        {
            if (_visualStudioHostFxiture != null)
            {
                _visualStudioHostFxiture.Dispose();
            }
        }
    }
}
