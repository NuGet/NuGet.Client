using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Apex
{
    public interface IVisualStudioHostFixtureFactory
    {
        VisualStudioHostFixture GetVisualStudioHostFixture();
    }
}
