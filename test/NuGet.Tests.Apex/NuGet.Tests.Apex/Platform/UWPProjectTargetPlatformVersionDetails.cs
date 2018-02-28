using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Tests.Apex.Util;

namespace NuGet.Tests.Apex.Platform
{
    /// <summary>
    /// Use to target UWP project.
    /// </summary>
    public static class UWPProjectTargetPlatformVersionDetails
    {
        private static string TargetPlatformMinVersion = Environment.GetEnvironmentVariable("Bliss_Predefined_TargetPlatformMinVersion");
        private static string TargetPlatformVersion = Environment.GetEnvironmentVariable("Bliss_Predefined_TargetPlatformVersion");

        /// <summary>
        /// Determines if the project needs to be updated to specified TargetPlatform(Min)Version environment variables
        /// </summary>
        /// <param name="project">The project to update</param>
        public static void UpdateProjectTargetVersions(UWPProjectTestExtension project)
        {
            string path = project.FullPath;

            string targetVersion = UWPProjectTargetPlatformVersionDetails.GetTargetVersionString(path);
            string targetMinVersion = UWPProjectTargetPlatformVersionDetails.GetTargetMinVersionString(path);

            bool updated = false;

            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            string currentVersion = doc.GetElementsByTagName(targetVersion).Item(0).InnerXml;
            string currentMinVersion = doc.GetElementsByTagName(targetMinVersion).Item(0).InnerXml;

            if (!string.IsNullOrWhiteSpace(UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion)
            && !string.Equals(currentMinVersion, UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion, StringComparison.OrdinalIgnoreCase))
            {
                doc.GetElementsByTagName(targetMinVersion).Item(0).InnerXml = UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion;
                project.TargetPlatformMinVersion = UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion)
            && !string.Equals(currentVersion, UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion, StringComparison.OrdinalIgnoreCase))
            {
                doc.GetElementsByTagName(targetVersion).Item(0).InnerXml = UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion;
                project.TargetPlatformVersion = UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion;
                updated = true;
            }

            if (updated)
            {
                project.Unload(saveProject: false);
                doc.Save(path);
                project.Reload();
                project.Clean();
            }
        }

        /// <summary>
        /// Determines if the project needs to be updated to specified TargetPlatform(Min)Version environment variables
        /// </summary>
        /// <param name="path">The path of the solution to update</param>
        public static void UpdateSolutionTargetVersions(string path)
        {
            foreach (string project in UWPProjectTargetPlatformVersionDetails.GetAllUapProjectsInSolution(path))
            {
                UWPProjectTargetPlatformVersionDetails.UpdateTargetVersionAtPath(project);
            }
        }

        public static void UpdateTargetVersionAtPath(string project)
        {
            bool updated = false;

            string targetVersion = UWPProjectTargetPlatformVersionDetails.GetTargetVersionString(project);
            string targetMinVersion = UWPProjectTargetPlatformVersionDetails.GetTargetMinVersionString(project);

            XmlDocument doc = new XmlDocument();
            doc.Load(project);
            string currentVersion = doc.GetElementsByTagName(targetVersion).Item(0).InnerXml;
            string currentMinVersion = doc.GetElementsByTagName(targetMinVersion).Item(0).InnerXml;

            if (!string.IsNullOrWhiteSpace(UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion)
            && !string.Equals(currentMinVersion, UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion, StringComparison.OrdinalIgnoreCase))
            {
                doc.GetElementsByTagName(targetMinVersion).Item(0).InnerXml = UWPProjectTargetPlatformVersionDetails.TargetPlatformMinVersion;
                updated = true;
            }

            // If predefined TPV is defined, use the predefined value no matter if the platform is installed on the machine
            if (!string.IsNullOrWhiteSpace(UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion))
            {
                if (!string.Equals(currentVersion, UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion, StringComparison.OrdinalIgnoreCase))
                {
                    doc.GetElementsByTagName(targetVersion).Item(0).InnerXml = UWPProjectTargetPlatformVersionDetails.TargetPlatformVersion;
                    updated = true;
                }
            }
            else if (!UAPSDKVersionHelper.GetUAPPlatformVersions().Contains<string>(currentVersion))
            {
                doc.GetElementsByTagName(targetVersion).Item(0).InnerXml = UAPSDKVersionHelper.GetLatestUAPVersion();
                updated = true;
            }

            if (updated)
            {
                doc.Save(project);
            }
        }

        public static IEnumerable<string> GetAllUapProjectsInSolution(string path)
        {
            SolutionFile solution = SolutionFile.Parse(path);

            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                if (UWPProjectTargetPlatformVersionDetails.ProjectIsUap(project.AbsolutePath))
                {
                    yield return project.AbsolutePath;
                }
            }
        }

        private static bool ProjectIsUap(string path)
        {
            string extension = Path.GetExtension(path);
            // DevDiv 283496: Solution folder is considered as a ProjectInSolution object. Skip it.
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            switch (extension)
            {
                case ".vbproj":
                case ".csproj":
                    string identifier = doc.GetElementsByTagName("TargetPlatformIdentifier")?.Item(0)?.InnerXml;
                    return string.Equals(identifier, "UAP", StringComparison.OrdinalIgnoreCase);
                case ".vcxproj":
                    string appType = doc.GetElementsByTagName("ApplicationType")?.Item(0)?.InnerXml;
                    string appTypeVersion = doc.GetElementsByTagName("ApplicationTypeRevision")?.Item(0)?.InnerXml;
                    return string.Equals(appType, "Windows Store", StringComparison.OrdinalIgnoreCase) && string.Equals(appTypeVersion, "10.0", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static string GetTargetVersionString(string path)
        {
            string extension = Path.GetExtension(path);
            switch (extension)
            {
                case ".vbproj":
                case ".csproj":
                    return "TargetPlatformVersion";
                case ".vcxproj":
                    return "WindowsTargetPlatformVersion";
                default:
                    throw new NotSupportedException(string.Format("{0} is not an expected UAP project extension.", extension));
            }
        }

        private static string GetTargetMinVersionString(string path)
        {
            string extension = Path.GetExtension(path);
            switch (extension)
            {
                case ".vbproj":
                case ".csproj":
                    return "TargetPlatformMinVersion";
                case ".vcxproj":
                    return "WindowsTargetPlatformMinVersion";
                default:
                    throw new NotSupportedException(string.Format("{0} is not an expected UAP project extension.", extension));
            }
        }
    }
}
