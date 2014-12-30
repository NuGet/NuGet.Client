using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.Resolver
{
    public static class Evaluate
    {
        //  A particular candidate solution can be tested back against the original metadata tree. The test is performed by a simple
        //  combination of two mutually recursive functions. One corresponds to every package registration (i.e. RegistrationInfo) and one
        //  corresponds to every package version (i.e. PackageInfo).
        //  For a solution to be valid a package from every registration mentioned in the dependency must be present but any version will work.
        //  (Note that at this stage the metadata tree has been trimmed to only include specific relevant package versions (i.e. the version ranges
        //  mentioned in the dependency have been applied and the results inlined in the tree as further branches.)

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
