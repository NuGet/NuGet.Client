using NuGet.Versioning;
using System;
using System.Globalization;
//using System.Reflection;

namespace NuGet.Protocol.Core.v2
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
                "CORE", // TODO: fix this
                host);
        }

        private static NuGetVersion GetNuGetVersion()
        {
            return new NuGetVersion(3, 0, 0, 0);
            //var attr = typeof(UserAgentUtil).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            //if (attr == null)
            //{
            //    return new NuGetVersion(3, 0, 0, 0);
            //}
            //return NuGetVersion.Parse(attr.InformationalVersion);
        }
    }
}
