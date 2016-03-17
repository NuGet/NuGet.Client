using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.Commands
{
    public interface IProjectFactory
    {
        Dictionary<string, string> GetProjectProperties();
        void SetIncludeSymbols(bool includeSymbols);
        PackageBuilder CreateBuilder(string basePath);
    }
}
