﻿using System;
using System.Globalization;

namespace NuGet.Frameworks
{
#if NUGET_FRAMEWORKS_INTERNAL
    internal
#else
    public
#endif
    class FrameworkRuntimePair : IEquatable<FrameworkRuntimePair>, IComparable<FrameworkRuntimePair>
    {
        public NuGetFramework Framework
        {
            get { return _framework; }
        }

        public string RuntimeIdentifier
        {
            get { return _runtimeIdentifier; }
        }

        public string Name
        {
            get { return _name; }
        }

        private readonly NuGetFramework _framework;
        private readonly string _runtimeIdentifier;
        private readonly string _name;

        public FrameworkRuntimePair(NuGetFramework framework, string runtimeIdentifier)
        {
            _framework = framework;
            _runtimeIdentifier = runtimeIdentifier ?? string.Empty;
            _name = GetName(framework, runtimeIdentifier);
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
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}~{1}",
                Framework.GetShortFolderName(),
                RuntimeIdentifier);
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
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1})",
                    framework,
                    runtimeIdentifier);
            }
        }
    }
}
