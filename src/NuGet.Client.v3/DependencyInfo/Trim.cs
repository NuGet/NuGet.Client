using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Client.DependencyInfo
{
    internal static class Trim
    {
        //  This code trims the metadata tree to just include packages that fit within an allowed-version specification.
        //  Packages outside of the range are removed from the tree. That removal should filter up the tree which is what the second
        //  pass is intended to achieve. This notion of allowed versions can trivially be extended for example to include
        //  for example multiple version ranges. (In fact this code could probably be parameterized with an actual trim lambda.)

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
            foreach (DependencyInfo dependencyInfo in packageInfo.Dependencies)
            {
                Pass1(dependencyInfo.RegistrationInfo, allowedVersion);
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
            foreach (DependencyInfo dependencyInfo in packageInfo.Dependencies)
            {
                Pass2(dependencyInfo.RegistrationInfo, ref updated);
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
            foreach (DependencyInfo dependencyInfo in packageInfo.Dependencies)
            {
                if (dependencyInfo.RegistrationInfo.Packages.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
