using System;
namespace NuGet.PackageManagement.VisualStudio
{
    public interface IEnvironmentVariableReader
    {
        string GetEnvironmentVariable(string variable);
    }
}
