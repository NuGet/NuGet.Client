using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.Resolver
{
    public static class Evaluate
    {
        public static bool Satisfy(RegistrationInfo registrationInfo, IEnumerable<KeyValuePair<string, NuGetVersion>> candidate)
        {
            IDictionary<string, NuGetVersion> dictionary = new Dictionary<string, NuGetVersion>();
            foreach (KeyValuePair<string, NuGetVersion> package in candidate)
            {
                dictionary.Add(package);
            }

            if (Satisfy(registrationInfo, dictionary))
            {
                return true;
            }
            return false;
        }

        static bool Satisfy(RegistrationInfo registrationInfo, IDictionary<string, NuGetVersion> dictionary)
        {
            if (registrationInfo.Packages.Count == 0)
            {
                return true;
            }

            // for a package ANY child can satisfy

            foreach (PackageInfo child in registrationInfo.Packages)
            {
                if (Satisfy(child, registrationInfo.Id, dictionary))
                {
                    return true;
                }
            }

            return false;
        }

        static bool Satisfy(PackageInfo packageInfo, string id, IDictionary<string, NuGetVersion> dictionary)
        {
            if (id == null || dictionary.Contains(new KeyValuePair<string, NuGetVersion>(id, packageInfo.Version)))
            {
                if (packageInfo.DependencyGroups.Count == 0)
                {
                    return true;
                }

                // for a particular version of a package ALL children must satisfy

                foreach (DependencyGroupInfo dependencyGroup in packageInfo.DependencyGroups)
                {
                    //TODO: pick appropriate dependency group

                    foreach (DependencyInfo dependency in dependencyGroup.Dependencies)
                    {
                        if (!Satisfy(dependency.RegistrationInfo, dictionary))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            return false;
        }
    }
}
