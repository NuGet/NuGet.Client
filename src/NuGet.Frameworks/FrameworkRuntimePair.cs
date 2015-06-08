using System;

namespace NuGet.Frameworks
{
    public class FrameworkRuntimePair : IEquatable<FrameworkRuntimePair>, IComparable<FrameworkRuntimePair>
    {
        public NuGetFramework Framework { get; }
        public string RuntimeIdentifier { get; }
        public string Name { get; }

        public FrameworkRuntimePair(NuGetFramework framework, string runtimeIdentifier)
        {
            Framework = framework;
            RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
            Name = GetName(framework, runtimeIdentifier);
        }

        public bool Equals(FrameworkRuntimePair other)
        {
            return other != null &&
                Equals(Framework, other.Framework) &&
                string.Equals(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FrameworkRuntimePair);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.GetHashCode(Framework, RuntimeIdentifier);
        }

        public override string ToString()
        {
            return $"{Framework.GetShortFolderName()}~{RuntimeIdentifier}";
        }

        public int CompareTo(FrameworkRuntimePair other)
        {
            var fxCompare = Framework.GetShortFolderName().CompareTo(other.Framework.GetShortFolderName());
            if(fxCompare != 0)
            {
                return fxCompare;
            }
            return string.Compare(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal);
        }

        public static string GetName(NuGetFramework framework, string runtimeIdentifier)
        {
            if (string.IsNullOrEmpty(runtimeIdentifier))
            {
                return framework.ToString();
            }
            else
            {
                return $"{framework} ({runtimeIdentifier})";
            }
        }
    }
}
