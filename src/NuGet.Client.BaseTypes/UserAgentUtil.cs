using NuGet.Versioning;
using System;
using System.Globalization;
using System.Reflection;

namespace NuGet.Client
{
    public static class UserAgentUtil
    {
        private static readonly Lazy<NuGetVersion> NuGetClientVersion = new Lazy<NuGetVersion>(GetNuGetVersion);

        private const string UserAgentFormat = "NuGet/{0} ({1}, {2}, {3})";

        public static string GetUserAgent(string context, string host)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                UserAgentFormat,
                NuGetClientVersion.Value.ToNormalizedString(),
                context,
                Environment.OSVersion,
                host);
        }

        private static NuGetVersion GetNuGetVersion()
        {
            var attr = typeof(UserAgentUtil).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attr == null)
            {
                return new NuGetVersion(3, 0, 0, 0);
            }
            return NuGetVersion.Parse(attr.InformationalVersion);
        }
    }
}
