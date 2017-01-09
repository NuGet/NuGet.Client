using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    public interface IPackageRule
    {
        IEnumerable<PackageIssue> Validate(PackageBuilder builder);
    }
}
