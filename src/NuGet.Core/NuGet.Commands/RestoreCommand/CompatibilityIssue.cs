using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet.Commands
{
    public class CompatibilityIssue : IEquatable<CompatibilityIssue>
    {
        public CompatibilityIssueType Type { get; }
        public NuGetFramework Framework { get; }
        public string RuntimeIdentifier { get; }
        public string AssemblyName { get; }
        public PackageIdentity Package { get; }
        public List<NuGetFramework> AvailableFrameworks { get; }

        private CompatibilityIssue(
            CompatibilityIssueType type,
            PackageIdentity package,
            string assemblyName,
            NuGetFramework framework,
            string runtimeIdentifier,
            IEnumerable<NuGetFramework> availableFrameworks)
        {
            Type = type;
            AssemblyName = assemblyName;
            Package = package;
            Framework = framework;
            RuntimeIdentifier = runtimeIdentifier;
            AvailableFrameworks = availableFrameworks.ToList();
        }

        public static CompatibilityIssue ReferenceAssemblyNotImplemented(string assemblyName, PackageIdentity referenceAssemblyPackage, NuGetFramework framework, string runtimeIdentifier)
        {
            return new CompatibilityIssue(
                CompatibilityIssueType.ReferenceAssemblyNotImplemented,
                referenceAssemblyPackage,
                assemblyName,
                framework,
                runtimeIdentifier,
                Enumerable.Empty<NuGetFramework>());
        }

        public static CompatibilityIssue IncompatiblePackage(
            PackageIdentity referenceAssemblyPackage,
            NuGetFramework framework,
            string runtimeIdentifier,
            IEnumerable<NuGetFramework> packageFrameworks)
        {
            return new CompatibilityIssue(
                CompatibilityIssueType.PackageIncompatible,
                referenceAssemblyPackage,
                string.Empty,
                framework,
                runtimeIdentifier,
                packageFrameworks);
        }

        public static CompatibilityIssue IncompatibleProject(
            PackageIdentity project,
            NuGetFramework framework,
            string runtimeIdentifier,
            IEnumerable<NuGetFramework> projectFrameworks)
        {
            return new CompatibilityIssue(
                CompatibilityIssueType.ProjectIncompatible,
                project,
                string.Empty,
                framework,
                runtimeIdentifier,
                projectFrameworks);
        }

        public override string ToString()
        {
            // NOTE(anurse): Why not just use Format's implementation as ToString? I feel like ToString should be
            // reserved for debug output, and just because they will be the same here doesn't mean it isn't useful
            // to have a separate method for the, semantically different, behavior of rendering a message for user
            // display, even though the results are the same.
            return Format();
        }

        public string Format()
        {
            if (Type == CompatibilityIssueType.ReferenceAssemblyNotImplemented)
            {
                if (string.IsNullOrEmpty(RuntimeIdentifier))
                {
                    return string.Format(CultureInfo.CurrentCulture, Strings.Log_MissingImplementationFx, Package.Id, Package.Version, AssemblyName, Framework);
                }

                return string.Format(CultureInfo.CurrentCulture, Strings.Log_MissingImplementationFxRuntime, Package.Id, Package.Version, AssemblyName, Framework, RuntimeIdentifier);
            }
            else if (Type == CompatibilityIssueType.PackageIncompatible)
            {
                var message = string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_PackageNotCompatibleWithFx,
                    Package.Id,
                    Package.Version.ToNormalizedString(),
                    FormatFramework(Framework, RuntimeIdentifier));

                var supports = string.Format(CultureInfo.CurrentCulture,
                            Strings.Log_PackageNotCompatibleWithFx_Supports,
                            Package.Id,
                            Package.Version.ToNormalizedString());

                var noSupports = string.Format(CultureInfo.CurrentCulture,
                            Strings.Log_PackageNotCompatibleWithFx_NoSupports,
                            Package.Id,
                            Package.Version.ToNormalizedString());

                return FormatMessage(message, supports, noSupports);
            }
            else if (Type == CompatibilityIssueType.ProjectIncompatible)
            {
                var message = string.Format(CultureInfo.CurrentCulture,
                        Strings.Log_ProjectNotCompatibleWithFx,
                        Package.Id,
                        FormatFramework(Framework, RuntimeIdentifier));

                var supports = string.Format(CultureInfo.CurrentCulture,
                            Strings.Log_ProjectNotCompatibleWithFx_Supports,
                            Package.Id);

                var noSupports = string.Format(CultureInfo.CurrentCulture,
                            Strings.Log_ProjectNotCompatibleWithFx_NoSupports,
                            Package.Id);

                return FormatMessage(message, supports, noSupports);
            }

            return null;
        }

        /// <summary>
        /// Build a incompatible error message for either a package or project
        /// </summary>
        private string FormatMessage(string message, string supports, string noSupports)
        {
            var sb = new StringBuilder();

            sb.Append(message);
            sb.Append(" ");

            if (AvailableFrameworks.Any())
            {
                sb.AppendFormat(supports);

                if (AvailableFrameworks.Count > 1)
                {
                    // Write multiple frameworks on new lines
                    foreach (var framework in AvailableFrameworks.Select(FormatFramework)
                        .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append($"  - {framework}");
                    }
                }
                else
                {
                    // Write single frameworks on the same line.
                    sb.Append($" {FormatFramework(AvailableFrameworks.Single())}");
                }
            }
            else
            {
                // No frameworks
                sb.Append(noSupports);
            }

            return sb.ToString();
        }

        private static string FormatFramework(NuGetFramework framework)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Log_FrameworkDisplay,
                framework.GetShortFolderName(),
                framework.DotNetFrameworkName);
        }

        private static string FormatFramework(NuGetFramework framework, string runtimeId)
        {
            if (string.IsNullOrEmpty(runtimeId))
            {
                return FormatFramework(framework);
            }
            else
            {
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_FrameworkRIDDisplay,
                    framework.GetShortFolderName(),
                    framework.DotNetFrameworkName,
                    runtimeId);
            }
        }

        public bool Equals(CompatibilityIssue other)
        {
            return other != null &&
                Equals(Type, other.Type) &&
                Equals(Framework, other.Framework) &&
                string.Equals(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal) &&
                string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal) &&
                Equals(Package, other.Package) &&
                new HashSet<NuGetFramework>(AvailableFrameworks, NuGetFramework.Comparer)
                    .SetEquals(other.AvailableFrameworks);
        }
    }

    public enum CompatibilityIssueType
    {
        ReferenceAssemblyNotImplemented,
        PackageIncompatible,
        ProjectIncompatible
    }
}