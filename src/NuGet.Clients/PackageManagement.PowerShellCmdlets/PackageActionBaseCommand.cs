// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Text;
using System.Threading;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Resolver;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class PackageActionBaseCommand : NuGetPowerShellBaseCommand
    {
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        protected readonly INuGetLockService _lockService;

        public PackageActionBaseCommand()
        {
            _deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();
            _lockService = ServiceLocator.GetInstance<INuGetLockService>();
        }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0)]
        public virtual string Id { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public virtual string ProjectName { get; set; }

        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty]
        public virtual string Version { get; set; }

        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty]
        public virtual string Source { get; set; }

        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        [Parameter]
        [Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        public SwitchParameter IgnoreDependencies { get; set; }

        [Parameter]
        public FileConflictAction? FileConflictAction { get; set; }

        [Parameter]
        public DependencyBehavior? DependencyVersion { get; set; }

        protected virtual void Preprocess()
        {
            CheckSolutionState();

            var result = ValidateSource(Source);
            if (result.Validity == SourceValidity.UnknownSource)
            {
                throw new PackageSourceException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UnknownSourceWithId,
                    Id,
                    result.Source));
            }
            else if (result.Validity == SourceValidity.UnknownSourceType)
            {
                throw new PackageSourceException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UnknownSourceType,
                    result.Source));
            }

            UpdateActiveSourceRepository(result.SourceRepository);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
            NuGetUIThreadHelper.JoinableTaskFactory.Run(CheckMissingPackagesAsync);
        }

        protected override void ProcessRecordCore()
        {
        }

        /// <summary>
        /// Install package by Identity
        /// </summary>
        /// <param name="project"></param>
        /// <param name="identity"></param>
        /// <param name="resolutionContext"></param>
        /// <param name="projectContext"></param>
        /// <param name="isPreview"></param>
        /// <param name="isForce"></param>
        /// <param name="uninstallContext"></param>
        /// <returns></returns>
        protected async Task InstallPackageByIdentityAsync(NuGetProject project, PackageIdentity identity, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview)
        {
            try
            {
                var actions = await PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext, PrimarySourceRepositories, null, CancellationToken.None);

                if (!actions.Any())
                {
                    // nuget operation status is set to NoOp to log under telemetry event
                    _status = NuGetOperationStatus.NoOp;
                }
                else
                {
                    // update packages count to be logged under telemetry event
                    _packageCount = actions.Select(action => action.PackageIdentity.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                }

                // stop telemetry event timer to avoid UI interaction
                TelemetryUtility.StopTimer();

                if (!ShouldContinueDueToDotnetDeprecation(actions, isPreview))
                {
                    // resume telemetry event timer after ui confirmation
                    TelemetryUtility.StartorResumeTimer();
                    return;
                }

                // resume telemetry event timer after ui confirmation
                TelemetryUtility.StartorResumeTimer();

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    NuGetPackageManager.SetDirectInstall(identity, projectContext);
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                    NuGetPackageManager.ClearDirectInstall(projectContext);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    // Set nuget operation status to NoOp for telemetry event when package
                    // is already installed.
                    _status = NuGetOperationStatus.NoOp;
                    Log(MessageLevel.Info, ex.Message);
                }
                else
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Install package by Id
        /// </summary>
        /// <param name="project"></param>
        /// <param name="packageId"></param>
        /// <param name="resolutionContext"></param>
        /// <param name="projectContext"></param>
        /// <param name="isPreview"></param>
        /// <param name="isForce"></param>
        /// <param name="uninstallContext"></param>
        /// <returns></returns>
        protected async Task InstallPackageByIdAsync(NuGetProject project, string packageId, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview)
        {
            try
            {
                var actions = await PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext, PrimarySourceRepositories, null, CancellationToken.None);

                if (!actions.Any())
                {
                    // nuget operation status is set to NoOp to log under telemetry event when
                    // there is no preview action.
                    _status = NuGetOperationStatus.NoOp;
                }
                else
                {
                    // update packages count to be logged under telemetry event
                    _packageCount = actions.Select(
                        action => action.PackageIdentity.Id).Distinct().Count();
                }

                // stop telemetry event timer to avoid UI interaction
                TelemetryUtility.StopTimer();

                if (!ShouldContinueDueToDotnetDeprecation(actions, isPreview))
                {
                    // resume telemetry event timer after ui confirmation
                    TelemetryUtility.StartorResumeTimer();
                    return;
                }

                // resume telemetry event timer after ui confirmation
                TelemetryUtility.StartorResumeTimer();

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    var identity = actions.Select(v => v.PackageIdentity).Where(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    NuGetPackageManager.SetDirectInstall(identity, projectContext);
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                    NuGetPackageManager.ClearDirectInstall(projectContext);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    // Set nuget operation status to NoOp for telemetry event when package
                    // is already installed.
                    _status = NuGetOperationStatus.NoOp;
                    Log(MessageLevel.Info, ex.Message);
                }
                else
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Normalize package Id input against server metadata for project K, which is case-sensitive.
        /// </summary>
        /// <param name="project"></param>
        protected void NormalizePackageId(NuGetProject project)
        {
            if (!(project is ProjectKNuGetProjectBase))
            {
                return;
            }

            var metadata = NuGetUIThreadHelper.JoinableTaskFactory.Run(() => GetPackagesFromRemoteSourceAsync(Id, includePrerelease: true));
            if (!metadata.Any())
            {
                return;
            }

            // note that we're assuming that package id is the same for all versions.
            Id = metadata.First().Identity.Id;
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();

            var packageDirectoriesMarkedForDeletion = _deleteOnRestartManager.GetPackageDirectoriesMarkedForDeletion();
            if (packageDirectoriesMarkedForDeletion != null && packageDirectoriesMarkedForDeletion.Count != 0)
            {
                _deleteOnRestartManager.CheckAndRaisePackageDirectoriesMarkedForDeletion();
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Cmdlet_RequestRestartToCompleteUninstall,
                    string.Join(", ", packageDirectoriesMarkedForDeletion));
                WriteWarning(message);
            }
        }

        protected bool ShouldContinueDueToDotnetDeprecation(IEnumerable<NuGetProjectAction> actions, bool whatIf)
        {
            // Don't prompt if the user has chosen to ignore this warning.
            // Also, -WhatIf should not prompt the user since there is not action performed anyway.
            if (DotnetDeprecatedPrompt.GetDoNotShowPromptState() || whatIf)
            {
                return true;
            }

            // Determine if any of the project actions are affected by the deprecated framework.
            var resolvedActions = actions.Select(action => new ResolvedAction(action.Project, action));

            var projects = DotnetDeprecatedPrompt.GetAffectedProjects(resolvedActions);
            if (!projects.Any())
            {
                return true;
            }

            var model = DotnetDeprecatedPrompt.GetDeprecatedFrameworkModel(projects);

            // Flush the existing messages (e.g. logging from the action preview).
            FlushBlockingCollection();

            // Prompt the user to determine if the project actions should be executed.
            var choices = new Collection<ChoiceDescription>
            {
                new ChoiceDescription(Resources.Cmdlet_No, Resources.Cmdlet_DeprecatedFrameworkNoHelp),
                new ChoiceDescription(Resources.Cmdlet_Yes, Resources.Cmdlet_DeprecatedFrameworkYesHelp),
                new ChoiceDescription(Resources.Cmdlet_YesDoNotPromptAgain, Resources.Cmdlet_DeprecatedFrameworkYesDoNotPromptAgainHelp)
            };

            var messageBuilder = new StringBuilder();
            messageBuilder.Append(model.TextBeforeLink);
            messageBuilder.Append(model.LinkText);
            messageBuilder.Append(" (");
            messageBuilder.Append(model.MigrationUrl);
            messageBuilder.Append(")");
            messageBuilder.Append(model.TextAfterLink);
            messageBuilder.Append(" ");
            messageBuilder.Append(Resources.Cmdlet_DeprecatedFrameworkContinue);

            WriteLine();
            var choice = Host.UI.PromptForChoice(
                string.Empty,
                messageBuilder.ToString(),
                choices,
                defaultChoice: 0);
            WriteLine();

            // Handle the response from the user.
            if (choice == 2)
            {
                // Yes and do not prompt again.
                DotnetDeprecatedPrompt.SaveDoNotShowPromptState(true);
                return true;
            }
            else if (choice == 1) // Yes
            {
                return true;
            }

            return false; // No
        }

        /// <summary>
        /// Determine file confliction action based on user input
        /// </summary>
        private void DetermineFileConflictAction()
        {
            if (FileConflictAction != null)
            {
                ConflictAction = FileConflictAction;
            }
        }

        /// <summary>
        /// Determine DependencyBehavior based on user input
        /// </summary>
        /// <returns></returns>
        protected virtual DependencyBehavior GetDependencyBehavior()
        {
            if (IgnoreDependencies.IsPresent)
            {
                return DependencyBehavior.Ignore;
            }
            if (DependencyVersion.HasValue)
            {
                return DependencyVersion.Value;
            }
            return GetDependencyBehaviorFromConfig();
        }

        /// <summary>
        /// Get the value of DependencyBehavior from NuGet.Config file
        /// </summary>
        /// <returns></returns>
        protected DependencyBehavior GetDependencyBehaviorFromConfig()
        {
            string dependencySetting = ConfigSettings.GetValue("config", "dependencyversion");
            DependencyBehavior behavior;
            bool success = Enum.TryParse(dependencySetting, true, out behavior);
            if (success)
            {
                return behavior;
            }
            // Default to Lowest
            return DependencyBehavior.Lowest;
        }
    }
}
