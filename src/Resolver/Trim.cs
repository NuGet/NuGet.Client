using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Resolver
{
    static class Trim
    {
        public static void TrimByAllowedVersions(RegistrationInfo registrationInfo, IDictionary<string, VersionRange> allowedVersions)
        {
            foreach (KeyValuePair<string, VersionRange> allowedVersion in allowedVersions)
            {
                Execute(registrationInfo, allowedVersion);
            }
        }

        static void Execute(RegistrationInfo registrationInfo, KeyValuePair<string, VersionRange> allowedVersion)
        {
            Pass1(registrationInfo, allowedVersion);

            bool updated = true;
            while (updated)
            {
                updated = false;
                Pass2(registrationInfo, ref updated);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //  Pass1

        static void Pass1(PackageInfo packageInfo, string id, KeyValuePair<string, VersionRange> allowedVersion)
        {
            foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
            {
                foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                {
                    Pass1(dependencyInfo.RegistrationInfo, allowedVersion);
                }
            }
        }

        static void Pass1(RegistrationInfo registrationInfo, KeyValuePair<string, VersionRange> allowedVersion)
        {
            if (registrationInfo.Id == allowedVersion.Key)
            {
                IList<PackageInfo> packagesToRemove = registrationInfo.Packages.Where(p => !allowedVersion.Value.Satisfies(p.Version)).ToList();

                foreach (PackageInfo packageToRemove in packagesToRemove)
                {
                    registrationInfo.Packages.Remove(packageToRemove);
                }
            }

            foreach (PackageInfo child in registrationInfo.Packages)
            {
                Pass1(child, registrationInfo.Id, allowedVersion);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //  Pass2

        static void Pass2(PackageInfo packageInfo, string id, ref bool updated)
        {
            foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
            {
                foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                {
                    Pass2(dependencyInfo.RegistrationInfo, ref updated);
                }
            }
        }

        static void Pass2(RegistrationInfo registrationInfo, ref bool updated)
        {
            IList<PackageInfo> packagesToRemove = new List<PackageInfo>();

            foreach (PackageInfo package in registrationInfo.Packages)
            {
                if (!CheckDependenciesExists(package))
                {
                    packagesToRemove.Add(package);
                }
            }

            foreach (PackageInfo packageToRemove in packagesToRemove)
            {
                registrationInfo.Packages.Remove(packageToRemove);
                updated = true;
            }

            foreach (PackageInfo child in registrationInfo.Packages)
            {
                Pass2(child, registrationInfo.Id, ref updated);
            }
        }

        static bool CheckDependenciesExists(PackageInfo packageInfo)
        {
            foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
            {
                foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                {
                    if (dependencyInfo.RegistrationInfo.Packages.Count == 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
