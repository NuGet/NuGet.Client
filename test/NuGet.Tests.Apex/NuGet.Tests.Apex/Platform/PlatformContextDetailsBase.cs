using System;
using System.ComponentModel.Design;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Debugger;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex.Platform
{
    internal abstract class PlatformContextDetailsBase
    {
        protected const int BreakModeSwitchDelaySeconds = 120;
        protected const int DebuggerModeSwitchDelaySeconds = 5000;

        private readonly object[] platformContextCapabilities = new object[Enum.GetValues(typeof(PlatformContextCapability)).Length];
        /// <summary>
        /// The default project template for this context
        /// </summary>
        protected abstract ProjectTemplate DefaultProjectTemplate { get; }

        /// <summary>
        /// The Apex-centric view of the programming language set for this context
        /// </summary>
        protected ProjectLanguage ProjectLanguage
        {
            get
            {
                return this.CodeLanguage.AsProjectLanguage();
            }
        }

        /// <summary>
        /// The Bliss-centric view of the programming language set for this context
        /// </summary>
        protected abstract CodeLanguage CodeLanguage { get; }

        /// <summary>
        /// The .NET framework used by this context. Can be null if .NET is not involved (i.e. CPP)
        /// </summary>
        protected abstract FrameworkName FrameworkName { get; }


        protected PlatformContextDetailsBase()
        {
        }

        public void Initialize()
        {
            this.RegisterPlatformContextCapabilities();
        }

        public virtual ProjectTestExtension CreateProject(VisualStudioHost host, string projectName, ProjectTemplate projectTemplate, bool addToExistingSolution = false, CodeLanguage codeLanguage = CodeLanguage.UnspecifiedLanguage)
        {
            if (projectTemplate == default(ProjectTemplate))
            {
                projectTemplate = this.DefaultProjectTemplate;
            }

            ProjectLanguage projectLanguage = codeLanguage == CodeLanguage.UnspecifiedLanguage ? this.ProjectLanguage : codeLanguage.AsProjectLanguage();

            ProjectTestExtension project = addToExistingSolution ?
                host.ObjectModel.Solution.AddProject(projectLanguage, projectTemplate, this.FrameworkName, projectName) :
                host.ObjectModel.Solution.CreateProject(projectLanguage, projectTemplate, this.FrameworkName, projectName);

            return project;
        }

        /// <summary>
        /// Adds and returns a test extension to an arbitrary item to the project.
        /// </summary>
        public virtual ProjectItemTestExtension AddItemToProject(ProjectTestExtension projectExtension, string itemFileName, ProjectItemTemplate itemTemplate)
        {
            return projectExtension.AddProjectItem(itemTemplate, this.ProjectLanguage, itemFileName);
        }

        /// <summary>
        /// Copies a file from the sourcePath to the destinationPath applying the specified replacements.
        /// </summary>
        /// <param name="sourcePath">Path from which the file should be copied.</param>
        /// <param name="destinationPath">Path to which the file should be copied.</param>
        /// <param name="replacements">
        /// Collection of replacement tuples.  Instances of each Item1 string in the file will be replaced with their corresponding Item2 string.
        /// </param>
        protected static void CopyFileWithReplacements(string sourcePath, string destinationPath, IEnumerable<Tuple<string, string>> replacements)
        {
            string projectFileText = File.ReadAllText(sourcePath);

            foreach (Tuple<string, string> replacement in replacements)
            {
                projectFileText = projectFileText.Replace(replacement.Item1, replacement.Item2);
            }

            File.WriteAllText(destinationPath, projectFileText);
        }

        /// <summary>
        /// Adds one project to the references list of another project
        /// </summary>
        public virtual void AddProjectReference(ProjectTestExtension mainProject, ProjectTestExtension referenceProject)
        {
            if (this.CodeLanguage == CodeLanguage.CPP)
            {
                using (AddReferenceDialogTestExtension addReferenceDialog = mainProject.References.Dialog)
                {
                    AddReferenceDialogTabTestExtension tab = null;
                    try
                    {
                        tab = addReferenceDialog.Tabs[(AddReferenceDialogTabId)AddReferenceDialogTabId.SolutionProjects];
                        if (tab.Verify.HasItem(referenceProject.Name))
                        {
                            AddReferenceDialogTabItemTestExtension item = tab.Items[referenceProject.Name];
                            item.Add();
                        }
                    }
                    finally
                    {
                        addReferenceDialog.Close();
                        tab.Infrastructure.Disconnect();
                    }
                }
            }
            else
            {
                mainProject.References.AddProjectReference(referenceProject);
            }
        }

        public virtual void BuildSolution(VisualStudioHost host)
        {
            BuildManagerService buildManagerService = host.ObjectModel.Solution.BuildManager;
            buildManagerService.Build(waitForBuildToFinish: true);
            buildManagerService.Verify.Succeeded();
        }

        public object GetCapabilityValue(PlatformContextCapability capability)
        {
            return this.platformContextCapabilities[(int)capability];
        }

        /// <summary>
        /// Determines if the platform context supports the specified PlatformCapability.
        /// </summary>
        public bool IsCapabilitySet(PlatformContextCapability capability)
        {
            object value = this.platformContextCapabilities[(int)capability];
            return (value is bool) && ((bool)value);
        }

        public void SetCapabilityValue(PlatformContextCapability capability, object value)
        {
            this.platformContextCapabilities[(int)capability] = value;
        }

        protected virtual void RegisterPlatformContextCapabilities()
        {
            this.SetCapabilityValue(PlatformContextCapability.SupportsInstallPackage, true);
            this.SetCapabilityValue(PlatformContextCapability.SupportsUnInstallPackage, true);
            this.SetCapabilityValue(PlatformContextCapability.SupportsUpgradePackage, true);
        }

        public virtual void RunDebugger(VisualStudioHost host)
        {
            host.ObjectModel.Debugger.Start();

            // DevDiv2:1090465 We need a few seconds to allow changing between DesignMode and DebugMode.
            ISynchronizationService synch = host.Get<ISynchronizationService>();
            bool timedOut = !synch.TryWaitFor(
                TimeSpan.FromSeconds(PlatformContextDetailsBase.DebuggerModeSwitchDelaySeconds),
                () => { return host.ObjectModel.Debugger.CurrentMode == DebuggerMode.RunMode; });

            if (timedOut)
            {
                string screenshotFileName = ContextImplementation.TakeScreenshot(host, "DeployFailure", "DeployFailure");
                throw new TimeoutException("Debugger timed out.");
            }
            else
            {
                host.ObjectModel.Debugger.Stop(TimeSpan.FromSeconds(PlatformContextDetailsBase.DebuggerModeSwitchDelaySeconds));
            }
        }

        public virtual void RunDebuggerAndBreak(VisualStudioHost host)
        {
            // DevDiv2:1090465 We need a few seconds to allow changing between DesignMode and DebugMode.
            host.ObjectModel.Debugger.Start(TimeSpan.FromSeconds(PlatformContextDetailsBase.DebuggerModeSwitchDelaySeconds));

            host.ObjectModel.Debugger.TryWaitForBreakMode(TimeSpan.FromSeconds(PlatformContextDetailsBase.BreakModeSwitchDelaySeconds));
            host.ObjectModel.Debugger.Verify.ModeIs(DebuggerMode.BreakMode);

            // Continue to execute the app.
            host.ObjectModel.Debugger.DisableAllBreakpoints();
            host.ObjectModel.Debugger.Continue();

            // Verify debugger is in runmode.
            host.ObjectModel.Debugger.Verify.ModeIs(DebuggerMode.RunMode);

            host.ObjectModel.Debugger.Stop(TimeSpan.FromSeconds(PlatformContextDetailsBase.DebuggerModeSwitchDelaySeconds));
        }

        public virtual SolutionService OpenSolution(VisualStudioHost host, string pathToSolution)
        {
            host.ObjectModel.Solution.Open(pathToSolution, SolutionLoadBehavior.Realtime);
            host.ObjectModel.Solution.ForceFullyLoaded();

            // We requre IDesignerEventService for perf markers when C# files are opened.
            // Forcing the solution to load doesn't guarentee IDesignerEventService is loaded so we force it here.
            host.ServiceProvider.GetService(typeof(IDesignerEventService));
            return host.ObjectModel.Solution;
        }
    }
}
