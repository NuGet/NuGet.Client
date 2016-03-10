using System;
using System.Globalization;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public class UserAgentStringBuilder
    {
        public static readonly string DefaultNuGetClientName = "NuGet Client V3";

        private const string UserAgentWithOSDescriptionAndVisualStudioSKUTemplate = "{0}/{1} ({2}, {3})";
        private const string UserAgentWithOSDescriptionTemplate = "{0}/{1} ({2})";
        private const string UserAgentTemplate = "{0}/{1}";

        private readonly string _clientInfo;
        private string _vsInfo;
        private string _osInfo;

        public UserAgentStringBuilder()
            : this(DefaultNuGetClientName)
        {
        }

        public UserAgentStringBuilder(string clientInfo)
        {
            _clientInfo = clientInfo;
            NuGetClientVersion = GetNuGetVersion();
        }

        public NuGetVersion NuGetClientVersion { get; }

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

            var clientInfo = _clientInfo;
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
                    _clientInfo,
                    NuGetClientVersion, /* NuGet version */
                    osDescription, /* OS version */
                    _vsInfo);  /* VS SKU + version */
            }
        }

        private static NuGetVersion GetNuGetVersion()
        {
            Version attr = null;

#if !DNXCORE50
            attr = typeof(Repository).Assembly.GetName().Version;
#endif

            if (attr == null)
            {
                return new NuGetVersion(3, 4, 0, 0);
            }

            NuGetVersion version;
            NuGetVersion.TryParse(attr.ToString(), out version);

            return version;
        }

        private string GetOSVersion()
        {
            if (_osInfo == null)
            {
#if !DNXCORE50
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