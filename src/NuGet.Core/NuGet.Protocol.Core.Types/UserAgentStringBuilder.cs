using System;
using System.Globalization;
using System.Reflection;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public class UserAgentStringBuilder
    {
        public static readonly string DefaultNuGetClientName = "NuGet Client V3";

        private const string UserAgentWithOSDescriptionAndVisualStudioSKUTemplate = "{0}/{1} ({2}, {3})";
        private const string UserAgentWithOSDescriptionTemplate = "{0}/{1} ({2})";
        private const string UserAgentTemplate = "{0}/{1}";

        private readonly string _clientName;
        private string _vsInfo;
        private string _osInfo;

        public UserAgentStringBuilder()
            : this(DefaultNuGetClientName)
        {
        }

        public UserAgentStringBuilder(string clientName)
        {
            _clientName = clientName;
            NuGetClientVersion = GetNuGetVersion();
        }

        public string NuGetClientVersion { get; }

        public UserAgentStringBuilder WithOSDescription(string osInfo)
        {
            _osInfo = osInfo;
            return this;
        }

        public UserAgentStringBuilder WithVisualStudioSKU(string vsInfo)
        {
            _vsInfo = vsInfo;
            return this;
        }

        public string Build()
        {
            var osDescription = _osInfo ?? GetOSVersion();

            var clientInfo = _clientName;
            if (NuGetTestMode.Enabled)
            {
                clientInfo = NuGetTestMode.NuGetTestClientName;
            }
            else if (string.IsNullOrEmpty(clientInfo))
            {
                clientInfo = DefaultNuGetClientName;
            }

            if (string.IsNullOrEmpty(osDescription))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    UserAgentTemplate,
                    clientInfo,
                    NuGetClientVersion);
            }

            if (string.IsNullOrEmpty(_vsInfo))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    UserAgentWithOSDescriptionTemplate,
                    clientInfo,
                    NuGetClientVersion,
                    osDescription);
            }
            else
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    UserAgentWithOSDescriptionAndVisualStudioSKUTemplate,
                    _clientName,
                    NuGetClientVersion, /* NuGet version */
                    osDescription, /* OS version */
                    _vsInfo);  /* VS SKU + version */
            }
        }

        private static string GetNuGetVersion()
        {
            var nugetVersion = string.Empty;

#if !IS_CORECLR
            var attr = typeof(Repository).Assembly.GetName().Version;

            NuGetVersion version;
            NuGetVersion.TryParse(attr.ToString(), out version);

            if (version != null)
            {
                nugetVersion = version.ToString();
            }
#else
            var assembly = typeof(Repository).GetTypeInfo().Assembly;
            var informationalVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationalVersionAttr != null)
            {
                nugetVersion = informationalVersionAttr.InformationalVersion;
            }
            else
            {
                var versionAttr = assembly.GetCustomAttribute<AssemblyVersionAttribute>();
                if (versionAttr != null)
                {
                    nugetVersion = versionAttr.Version.ToString();
                }
            }

#endif

            return nugetVersion;
        }

        private string GetOSVersion()
        {
            if (_osInfo == null)
            {
#if !IS_CORECLR
                // When not on CoreClr and no OSDescription was provided,
                // we will set it ourselves.
                _osInfo = Environment.OSVersion.ToString();
#else
                // When on CoreClr, one should use the .WithOSDescription() method to set it.
                _osInfo = string.Empty;
#endif
            }

            return _osInfo;
        }
    }
}