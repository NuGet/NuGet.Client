using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class NuGetFramework : IEquatable<NuGetFramework>
    {
        private readonly string _frameworkName;
        private readonly Version _version;
        private readonly string _profile;
        private const string _portable = "portable";
        private readonly static Version _emptyVersion = new Version(0, 0);

        public NuGetFramework(string framework)
            : this(framework, _emptyVersion)
        {

        }

        public NuGetFramework(string framework, Version version)
            : this(framework, version, null)
        {

        }

        public NuGetFramework(string framework, Version version, string profile)
        {
            _frameworkName = framework;
            _version = version;
            _profile = profile;
        }

        public string Framework
        {
            get
            {
                return _frameworkName;
            }
        }

        public Version Version
        {
            get
            {
                return _version;
            }
        }

        public string Profile
        {
            get
            {
                return _profile;
            }
        }

        public string FullFrameworkName
        {
            get
            {
                List<string> parts = new List<string>(3) { Framework };

                if (Version != _emptyVersion)
                {
                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Version={0}", Version.ToString()));
                }
                else
                {
                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Version=0.0", Version.ToString()));
                }

                if (Profile != null)
                {
                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Profile={0}", Profile));
                }

                return String.Join(", ", parts);
            }
        }

        public override string ToString()
        {
            return FullFrameworkName;
        }

        public IEnumerable<NuGetFramework> CompatibleFrameworks
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool Equals(NuGetFramework other)
        {
            throw new NotImplementedException();
        }

        public static readonly NuGetFramework UnsupportedFramework = new NuGetFramework("Unsupported");
        public static readonly NuGetFramework EmptyFramework = new NuGetFramework(string.Empty);
        public static readonly NuGetFramework AnyFramework = new NuGetFramework("Any");

        public bool IsUnsupported
        {
            get
            {
                return this == UnsupportedFramework;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return this == EmptyFramework;
            }
        }

        public bool IsAny
        {
            get
            {
                return this == AnyFramework;
            }
        }

        public bool IsSpecificFramework
        {
            get
            {
                return !IsEmpty && !IsAny && !IsUnsupported;
            }
        }

        public static NuGetFramework Parse(string folderName)
        {
            return Parse(folderName, DefaultFrameworkNameProvider.Instance);
        }

        public static NuGetFramework Parse(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException("folderName");
            }

            NuGetFramework result = UnsupportedFramework;

            Match match = FrameworkConstants.FrameworkRegex.Match(folderName);

            if (match.Success)
            {
                string framework = mappings.GetIdentifier(match.Groups["Framework"].Value);

                if (!String.IsNullOrEmpty(framework))
                {
                    Version version = mappings.GetVersion(match.Groups["Version"].Value);

                    // make sure we have a valid version or none at all
                    if (version != null)
                    {
                        string profile = match.Groups["Profile"].Value.TrimStart('-');

                        if (StringComparer.OrdinalIgnoreCase.Equals(".NETPortable", framework))
                        {
                            // TODO: Find the Profile number
                            result = new NuGetFramework(framework, version, null);
                        }
                        else
                        {
                            result = new NuGetFramework(framework, version, profile);
                        }
                    }
                }
            }

            return result;
        }
    }
}
