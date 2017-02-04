using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface ICredentialServiceProvider
    {
        NuGet.Configuration.ICredentialService GetCredentialService();
    }
}