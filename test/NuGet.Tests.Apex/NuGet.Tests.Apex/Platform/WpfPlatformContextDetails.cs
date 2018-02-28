using System;
using System.Runtime.Versioning;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex.Platform
{
    internal class WpfPlatformContextDetails : PlatformContextDetailsBase
    {
        private readonly CodeLanguage codeLanguage;
        private readonly FrameworkName frameworkName;
        private readonly PlatformVersion platformVersion;

        public WpfPlatformContextDetails(Context context)
        {
            if (!(context.Language == CodeLanguage.CSharp || context.Language == CodeLanguage.VB || context.Language == CodeLanguage.UnspecifiedLanguage))
            {
                throw new ArgumentException("codeLanguage");
            }

            this.codeLanguage = context.Language;
            this.platformVersion = context.Version;

            switch (this.platformVersion)
            {
                case PlatformVersion.v_4_5:
                    this.frameworkName = new FrameworkName(".NETFramework", new Version(4, 5));
                    break;
                case PlatformVersion.v_4_5_1:
                    this.frameworkName = new FrameworkName(".NETFramework", new Version(4, 5, 1));
                    break;
                case PlatformVersion.v_4_5_2:
                    this.frameworkName = new FrameworkName(".NETFramework", new Version(4, 5, 2));
                    break;
                case PlatformVersion.v_4_6:
                    this.frameworkName = new FrameworkName(".NETFramework", new Version(4, 6));
                    break;
                case PlatformVersion.v_4_6_1:
                    this.frameworkName = new FrameworkName(".NETFramework", new Version(4, 6, 1));
                    break;
                default:
                    throw new ArgumentException("platformVersion");
            }
        }

        protected override ProjectTemplate DefaultProjectTemplate { get { return ProjectTemplate.WPFApplication; } }

        protected override CodeLanguage CodeLanguage { get { return this.codeLanguage; } }

        protected override FrameworkName FrameworkName { get { return this.frameworkName; } }
    }
}
