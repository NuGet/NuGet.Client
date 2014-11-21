using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IFrameworkNameProvider
    {
        string GetIdentifier(string identifierShortName);

        string GetShortIdentifier(string identifier);

        string GetProfile(string profileShortName);

        string GetShortProfile(string profile);

        Version GetVersion(string versionString);

        string GetVersionString(Version version);
    }
}
