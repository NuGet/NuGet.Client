using System;
using System.Runtime.Versioning;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex.Platform
{
    /// <summary>
    /// Use to target UWP project.
    /// </summary>
    abstract class WindowsImmersivePlatformContextDetails : PlatformContextDetailsBase
    {
        private readonly CodeLanguage codeLanguage;
        private readonly FrameworkName frameworkName;
        private readonly PlatformVersion platformVersion;

        protected PlatformVersion PlatformVersion { get { return this.platformVersion; } }
        protected override CodeLanguage CodeLanguage { get { return this.codeLanguage; } }
        protected override FrameworkName FrameworkName { get { return this.frameworkName; } }

        public WindowsImmersivePlatformContextDetails(Context context)
        {
            this.codeLanguage = context.Language;
            this.frameworkName = this.ConvertPlatformVersionToFrameworkName(context.Version);
            this.platformVersion = context.Version;
        }

        protected virtual FrameworkName ConvertPlatformVersionToFrameworkName(PlatformVersion platformVersion)
        {
            FrameworkName frameworkName = null;
            if (codeLanguage != CodeLanguage.CPP)
            {
                switch (platformVersion)
                {
                    case PlatformVersion.v_8_0:
                        frameworkName = new FrameworkName(".NETCore", new Version(4, 5));
                        break;
                    case PlatformVersion.v_8_1:
                        frameworkName = new FrameworkName(".NETCore", new Version(4, 5, 1));
                        break;
                    default:
                        throw new ArgumentException("platformVersion");
                }
            }
            return frameworkName;
        }
    }
}
