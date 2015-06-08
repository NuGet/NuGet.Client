using System;
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

        private CompatibilityIssue(CompatibilityIssueType type, PackageIdentity package, string assemblyName, NuGetFramework framework, string runtimeIdentifier)
        {
            Type = type;
            AssemblyName = assemblyName;
            Package = package;
            Framework = framework;
            RuntimeIdentifier = runtimeIdentifier;
        }

        public static CompatibilityIssue ReferenceAssemblyNotImplemented(string assemblyName, PackageIdentity referenceAssemblyPackage, NuGetFramework framework, string runtimeIdentifier)
        {
            return new CompatibilityIssue(CompatibilityIssueType.ReferenceAssemblyNotImplemented, referenceAssemblyPackage, assemblyName, framework, runtimeIdentifier);
        }

        public static CompatibilityIssue Incompatible(PackageIdentity referenceAssemblyPackage, NuGetFramework framework, string runtimeIdentifier)
        {
            return new CompatibilityIssue(CompatibilityIssueType.PackageIncompatible, referenceAssemblyPackage, string.Empty, framework, runtimeIdentifier);
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
            switch (Type)
            {
                case CompatibilityIssueType.ReferenceAssemblyNotImplemented:
                    if (string.IsNullOrEmpty(RuntimeIdentifier))
                    {
                        return Strings.FormatLog_MissingImplementationFx(Package.Id, Package.Version, AssemblyName, Framework);
                    }
                    return Strings.FormatLog_MissingImplementationFxRuntime(Package.Id, Package.Version, AssemblyName, Framework, RuntimeIdentifier);
                case CompatibilityIssueType.PackageIncompatible:
                    return Strings.FormatLog_PackageNotCompatibleWithFx(Package.Id, Package.Version, FrameworkRuntimePair.GetName(Framework, RuntimeIdentifier));
                default:
                    return null;
            }
        }

        public bool Equals(CompatibilityIssue other)
        {
            return other != null &&
                Equals(Type, other.Type) &&
                Equals(Framework, other.Framework) &&
                string.Equals(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal) &&
                string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal) &&
                Equals(Package, other.Package);  
        }
    }

    public enum CompatibilityIssueType
    {
        ReferenceAssemblyNotImplemented,
        PackageIncompatible
    }
}