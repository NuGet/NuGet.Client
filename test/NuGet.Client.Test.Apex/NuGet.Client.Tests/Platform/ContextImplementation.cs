using System;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Shell;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Integration.Platform
{
    /// <summary>
    /// Provides a single entry point for Apex-centric operations on a given Bliss test context.
    /// </summary>
    public class ContextImplementation
    {
        private static readonly TimeSpan DefaultDocumentOpenTimeout = TimeSpan.FromSeconds(2);
        private const int DebuggerTimeoutMilliseconds = 10000;
        private static readonly TimeSpan DefaultDebuggerChangeStatusTimeout = TimeSpan.FromMinutes(5);
        private readonly Context context;
        private readonly PlatformContextDetailsBase platformDetails;

        public ContextImplementation(Context context)
        {
            this.context = context;

            PlatformContextDetailsBase platformDetails;
            switch (context.Platform)
            {
                case PlatformIdentifier.Wpf:
                    platformDetails = new WpfPlatformContextDetails(context);
                    break;
                case PlatformIdentifier.UWP:
                    platformDetails = new UWPPlatformContextDetails(context);
                    break;
                default:
                    throw new ArgumentException("context");
            }
            this.platformDetails = platformDetails;
            this.platformDetails.Initialize();
        }

        public static string TakeScreenshot(VisualStudioHost host, string categoryName, string fileName)
        {
            IScreenshotService screenshotService = host.Get<IScreenshotService>();
            if (screenshotService != null)
            {
                string guid = Guid.NewGuid().ToString();
                string screenshotFileName = string.Format(
                    "{0}_{1}",
                    fileName,
                    guid);
                screenshotService.TakeOne("NuGetClient" + categoryName, screenshotFileName);

                return screenshotFileName;
            }

            return string.Empty;
        }

        public static string TakeScreenshot(VisualStudioHost host, string categoryName, string fileName, string repositoryPath)
        {
            IScreenshotService screenshotService = host.Get<IScreenshotService>();
            if (screenshotService != null)
            {
                string screenshotFileName = screenshotService.TakeOne(categoryName, fileName, repositoryPath);
                return screenshotFileName;
            }

            return string.Empty;
        }

        public ProjectTestExtension CreateProject(VisualStudioHost host, ProjectTemplate projectTemplate = default(ProjectTemplate))
        {
            return this.platformDetails.CreateProject(host, null, projectTemplate);
        }


        private string argument;

        /// <summary>
        /// NuGet Package Manager command GUID
        /// </summary>
        private readonly Guid nugetPackageManagerCmdSet_guid = new Guid("25fd982b-8cae-4cbd-a440-e03ffccde106");


        /// <summary>
        /// NuGet Package Manager identifier
        /// </summary>
        private readonly uint cmdidPackageManagerDialog = 0x100;

        public CommandingService Command { get; set; }

        public void OpenPackageManager()
        {
            this.Command.ExecuteCommand(this.nugetPackageManagerCmdSet_guid, cmdidPackageManagerDialog, argument);
        }
    }
}
