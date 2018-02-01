using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Test.Apex.VisualStudio.Skus;
using NuGetClient.Test.Foundation;
using Omni.Common.VS;

namespace NuGetClient.Test.Integration.Util
{
    public sealed class VisualStudioInfo
    {
        private static readonly string CpvsbuildLocation = @"\\cpvsbuild\drops\VS\{0}\raw\{1}.{2:00}\binaries.x86ret";
        private static readonly string CpvsbuildCurrentLocation = @"\\cpvsbuild\drops\VS\{0}\raw\current\binaries.x86ret";
        private static readonly string BlendExePath = Path.Combine(VisualStudioSetup.CommonIdePath, "blend.exe");
        private static readonly string DevenvExePath = Path.Combine(VisualStudioSetup.CommonIdePath, "devenv.exe");

        // We cannot make APEX calls until the APEX assembly resolver has been set up, so make this lazy
        private static readonly Lazy<string> DevEnvExePath = new Lazy<string>(() => VisualStudioHostSkuFactory.Create(VisualStudioHostSkuFactory.GetDefaultSku()).ExecutablePath);

        // The path the Blend does not change between SKUs, so use that to determine the file version for the branch info
        private FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(BlendExePath);
        private Version vsVersion;
        private string buildShare;
        private string branch;
        private string mostRecentBuildShare;

        private VisualStudioInfo()
        {
            if (string.IsNullOrWhiteSpace(VisualStudioSetup.CommonIdePath) || !File.Exists(BlendExePath) || !File.Exists(DevenvExePath))
            {
                throw new InvalidOperationException("Could not find VisualStudio installation");
            }

            Environment.SetEnvironmentVariable(InstallationLocator.EnvironmentVariables.VisualStudioInstallationUnderTestPath, VisualStudioSetup.InstallationPath);
        }

        public static string Branch { get { return Nested.instance.BuiltBy; } }
        public static string BuildShare { get { return Nested.instance.CpvsBuildShare; } }
        public static string CurrentBuildShare { get { return Nested.instance.MostRecentBuildShare; } }
        public static Version Version { get { return Nested.instance.ExeVersion; } }
        public static string DevenvPath { get { return Nested.instance.PathToDevenvExecutable; } }
        public static string BlendPath { get { return Nested.instance.PathToBlendExecutable; } }
        public static string VsInstallRootPath { get { return Directory.GetParent(DevenvPath).Parent.Parent.FullName; } }
        public string PathToDevenvExecutable { get { return VisualStudioInfo.DevEnvExePath.Value; } }
        public string PathToBlendExecutable { get { return VisualStudioInfo.BlendExePath; } }

        public string BuiltBy
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.branch))
                {
                    // e.g. 14.0.22716.0 built by: VSCLIENT_1
                    this.branch = Environment.GetEnvironmentVariable("BlissTest_Branch") ??
                        fileVersionInfo.FileVersion.Substring(fileVersionInfo.FileVersion.LastIndexOf(':') + 1).Trim();
                }
                return this.branch;
            }
        }

        public Version ExeVersion
        {
            get
            {
                if (this.vsVersion == null)
                {
                    string productVerson = Environment.GetEnvironmentVariable("BlissTest_Version") ??
                        fileVersionInfo.ProductVersion;
                    if (!Version.TryParse(productVerson, out vsVersion))
                    {
                        throw new InvalidOperationException("Could not find product version of installed VisualStudio");
                    }
                }
                return vsVersion;
            }
        }

        public string CpvsBuildShare
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.buildShare))
                {
                    Version version = this.ExeVersion;
                    this.buildShare = Environment.GetEnvironmentVariable("BlissTest_BuildShare") ??
                        string.Format(CultureInfo.InvariantCulture, VisualStudioInfo.CpvsbuildLocation, this.BuiltBy, version.Build, version.Revision);
                }
                return this.buildShare;
            }
        }

        public string MostRecentBuildShare
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.mostRecentBuildShare))
                {
                    this.mostRecentBuildShare = Environment.GetEnvironmentVariable("BlissTest_CurrentBuildShare") ??
                        string.Format(CultureInfo.InvariantCulture, VisualStudioInfo.CpvsbuildCurrentLocation, this.BuiltBy);
                }
                return this.mostRecentBuildShare;
            }
        }

        public static VisualStudioInfo Instance { get { return Nested.instance; } }

        private class Nested
        {
            static Nested() { }

            internal static readonly VisualStudioInfo instance = new VisualStudioInfo();
        }
    }
}
