using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Resources;
using NuGetConsole;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsTemplateWizard))]
    public class VsTemplateWizard : IVsTemplateWizard
    {
        private const string DefaultRepositoryDirectory = "packages";
        private readonly IVsPackageInstaller _installer;
        private IEnumerable<PreinstalledPackageConfiguration> _configurations;

        private DTE _dte;
        private readonly IVsPackageInstallerServices _packageServices;
        private readonly IOutputConsoleProvider _consoleProvider;
        private readonly ISolutionManager _solutionManager;
        private readonly PreinstalledPackageInstaller _preinstalledPackageInstaller;
        private readonly ISettings _settings;
        private readonly ISourceRepositoryProvider _sourceProvider;

        [ImportingConstructor]
        public VsTemplateWizard(
            IVsPackageInstaller installer,
            IVsPackageInstallerServices packageServices,
            IOutputConsoleProvider consoleProvider,
            ISolutionManager solutionManager,
            ISettings settings,
            ISourceRepositoryProvider sourceProvider
            )
        {
            _installer = installer;
            _packageServices = packageServices;
            _consoleProvider = consoleProvider;
            _solutionManager = solutionManager;
            _settings = settings;
            _sourceProvider = sourceProvider;

            _preinstalledPackageInstaller = new PreinstalledPackageInstaller(_packageServices, _solutionManager, _settings, _sourceProvider, (VsPackageInstaller)_installer);
        }

        private IEnumerable<PreinstalledPackageConfiguration> GetConfigurationsFromVsTemplateFile(string vsTemplatePath)
        {
            XDocument document = LoadDocument(vsTemplatePath);

            return GetConfigurationsFromXmlDocument(document, vsTemplatePath);
        }

        internal IEnumerable<PreinstalledPackageConfiguration> GetConfigurationsFromXmlDocument(
            XDocument document,
            string vsTemplatePath,
            object vsExtensionManager = null,
            IEnumerable<IRegistryKey> registryKeys = null)
        {
            // Ignore XML namespaces since VS does not check them either when loading vstemplate files.
            IEnumerable<XElement> packagesElements = document.Root.ElementsNoNamespace("WizardData")
                .ElementsNoNamespace("packages");

            foreach (var packagesElement in packagesElements)
            {
                IList<PreinstalledPackageInfo> packages = new PreinstalledPackageInfo[0];
                string repositoryPath = null;
                bool isPreunzipped = false;

                string isPreunzippedString = packagesElement.GetOptionalAttributeValue("isPreunzipped");
                if (!String.IsNullOrEmpty(isPreunzippedString))
                {
                    Boolean.TryParse(isPreunzippedString, out isPreunzipped);
                }

                packages = GetPackages(packagesElement).ToList();

                if (packages.Count > 0)
                {
                    RepositoryType repositoryType = GetRepositoryType(packagesElement);
                    repositoryPath = GetRepositoryPath(packagesElement, repositoryType, vsTemplatePath, vsExtensionManager, registryKeys);
                }

                yield return new PreinstalledPackageConfiguration(repositoryPath, packages, isPreunzipped);
            }
        }

        private IEnumerable<PreinstalledPackageInfo> GetPackages(XElement packagesElement)
        {
            var declarations = (from packageElement in packagesElement.ElementsNoNamespace("package")
                                let id = packageElement.GetOptionalAttributeValue("id")
                                let version = packageElement.GetOptionalAttributeValue("version")
                                let skipAssemblyReferences = packageElement.GetOptionalAttributeValue("skipAssemblyReferences")
                                let includeDependencies = packageElement.GetOptionalAttributeValue("includeDependencies")
                                select new { id, version, skipAssemblyReferences, includeDependencies }).ToList();

            NuGetVersion semVer = null;
            bool skipAssemblyReferencesValue;
            bool includeDependenciesValue;
            var missingOrInvalidAttributes = from declaration in declarations
                                             where
                                                 String.IsNullOrWhiteSpace(declaration.id) ||
                                                 String.IsNullOrWhiteSpace(declaration.version) ||
                                                 !NuGetVersion.TryParse(declaration.version, out semVer) ||
                                                 (declaration.skipAssemblyReferences != null &&
                                                  !Boolean.TryParse(declaration.skipAssemblyReferences, out skipAssemblyReferencesValue)) ||
                                                 (declaration.includeDependencies != null &&
                                                  !Boolean.TryParse(declaration.includeDependencies, out includeDependenciesValue))

                                             select declaration;

            if (missingOrInvalidAttributes.Any())
            {
                ShowErrorMessage(
                    VsResources.TemplateWizard_InvalidPackageElementAttributes);
                throw new WizardBackoutException();
            }

            return from declaration in declarations
                   select new PreinstalledPackageInfo(
                       declaration.id,
                       declaration.version,
                       skipAssemblyReferences: declaration.skipAssemblyReferences != null && Boolean.Parse(declaration.skipAssemblyReferences),

                       // Note that the declaration uses "includeDependencies" but we need to invert it to become ignoreDependencies
                       // The declaration uses includeDependencies so that the default value can be 'false'
                       ignoreDependencies: !(declaration.includeDependencies != null && Boolean.Parse(declaration.includeDependencies))
                    );
        }

        private string GetRepositoryPath(
            XElement packagesElement,
            RepositoryType repositoryType,
            string vsTemplatePath,
            object vsExtensionManager,
            IEnumerable<IRegistryKey> registryKeys)
        {
            switch (repositoryType)
            {
                case RepositoryType.Template:
                    return Path.GetDirectoryName(vsTemplatePath);

                case RepositoryType.Extension:
                    return GetExtensionRepositoryPath(packagesElement, vsExtensionManager);

                case RepositoryType.Registry:
                    return GetRegistryRepositoryPath(packagesElement, registryKeys);
            }
            // should not happen
            return null;
        }

        private string GetExtensionRepositoryPath(XElement packagesElement, object vsExtensionManager)
        {
            string repositoryId = packagesElement.GetOptionalAttributeValue("repositoryId");
            if (repositoryId == null)
            {
                ShowErrorMessage(VsResources.TemplateWizard_MissingExtensionId);
                throw new WizardBackoutException();
            }

            return _preinstalledPackageInstaller.GetExtensionRepositoryPath(repositoryId, vsExtensionManager, ThrowWizardBackoutError);
        }

        private string GetRegistryRepositoryPath(XElement packagesElement, IEnumerable<IRegistryKey> registryKeys)
        {
            string keyName = packagesElement.GetOptionalAttributeValue("keyName");
            if (String.IsNullOrEmpty(keyName))
            {
                ShowErrorMessage(VsResources.TemplateWizard_MissingRegistryKeyName);
                throw new WizardBackoutException();
            }

            return _preinstalledPackageInstaller.GetRegistryRepositoryPath(keyName, registryKeys, ThrowWizardBackoutError);
        }

        private RepositoryType GetRepositoryType(XElement packagesElement)
        {
            string repositoryAttributeValue = packagesElement.GetOptionalAttributeValue("repository");
            switch (repositoryAttributeValue)
            {
                case "extension":
                    return RepositoryType.Extension;

                case "registry":
                    return RepositoryType.Registry;

                case "template":
                case null:
                    return RepositoryType.Template;

                default:
                    ShowErrorMessage(String.Format(VsResources.TemplateWizard_InvalidRepositoryAttribute,
                        repositoryAttributeValue));
                    throw new WizardBackoutException();
            }
        }

        internal virtual XDocument LoadDocument(string path)
        {
            return XmlUtility.LoadSafe(path);
        }

        private void ProjectFinishedGenerating(Project project)
        {
            TemplateFinishedGenerating(project);
        }

        private void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            TemplateFinishedGenerating(projectItem.ContainingProject);
        }

        private void TemplateFinishedGenerating(Project project)
        {
            foreach (var configuration in _configurations)
            {
                if (configuration.Packages.Any())
                {
                    _preinstalledPackageInstaller.PerformPackageInstall(_installer, project, configuration, ShowWarningMessage, ShowErrorMessage);
                }
            }
        }

        private void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            if (runKind != WizardRunKind.AsNewProject && runKind != WizardRunKind.AsNewItem)
            {
                ShowErrorMessage(VsResources.TemplateWizard_InvalidWizardRunKind);
                throw new WizardBackoutException();
            }

            _dte = (DTE)automationObject;
            _preinstalledPackageInstaller.InfoHandler = message => _dte.StatusBar.Text = message;

            if (customParams.Length > 0)
            {
                var vsTemplatePath = (string)customParams[0];
                _configurations = GetConfigurationsFromVsTemplateFile(vsTemplatePath);
            }

            if (replacementsDictionary != null)
            {
                AddTemplateParameters(replacementsDictionary);
            }
        }

        private void AddTemplateParameters(Dictionary<string, string> replacementsDictionary)
        {
            // add the $nugetpackagesfolder$ parameter which returns relative path to the solution's packages folder.
            // this is used by project templates to include assembly references directly inside the template project file
            // without relying on nuget to install the actual packages.
            string targetInstallDir;
            if (replacementsDictionary.TryGetValue("$destinationdirectory$", out targetInstallDir))
            {
                string solutionRepositoryPath = null;
                if (_dte.Solution != null && _dte.Solution.IsOpen)
                {
                    //solutionRepositoryPath = RepositorySettings.Value.RepositoryPath;
                    solutionRepositoryPath = PackagesFolderPathUtility.GetPackagesFolderPath(_solutionManager, _settings);
                }
                else
                {
                    string solutionDir = DetermineSolutionDirectory(replacementsDictionary);
                    if (!String.IsNullOrEmpty(solutionDir))
                    {
                        // If the project is a Website that is created on an Http location,
                        // solutionDir may be an Http address, e.g. http://localhost.
                        // In that case, we have to use forward slash instead of backward one.
                        if (Uri.IsWellFormedUriString(solutionDir, UriKind.Absolute))
                        {
                            solutionRepositoryPath = PathUtility.EnsureTrailingForwardSlash(solutionDir) + DefaultRepositoryDirectory;
                        }
                        else
                        {
                            solutionRepositoryPath = Path.Combine(solutionDir, DefaultRepositoryDirectory);
                        }
                    }
                }

                if (solutionRepositoryPath != null)
                {
                    // If the project is a Website that is created on an Http location,
                    // targetInstallDir may be an Http address, e.g. http://localhost.
                    // In that case, we have to use forward slash instead of backward one.
                    if (Uri.IsWellFormedUriString(targetInstallDir, UriKind.Absolute))
                    {
                        targetInstallDir = PathUtility.EnsureTrailingForwardSlash(targetInstallDir);
                    }
                    else
                    {
                        targetInstallDir = PathUtility.EnsureTrailingSlash(targetInstallDir);
                    }

                    replacementsDictionary["$nugetpackagesfolder$"] =
                        PathUtility.EnsureTrailingSlash(PathUtility.GetRelativePath(targetInstallDir, solutionRepositoryPath));
                }
            }

            // provide a current timpestamp (for use by universal provider)
            replacementsDictionary["$timestamp$"] = DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        internal virtual void ThrowWizardBackoutError(string message)
        {
            ShowErrorMessage(message);
            throw new WizardBackoutException();
        }

        internal virtual void ShowErrorMessage(string message)
        {
            MessageHelper.ShowErrorMessage(message, VsResources.TemplateWizard_ErrorDialogTitle);
        }

        internal virtual void ShowWarningMessage(string message)
        {
            IConsole console = _consoleProvider.CreateOutputConsole(requirePowerShellHost: false);
            console.WriteLine(message);
        }

        void IWizard.BeforeOpeningFile(ProjectItem projectItem)
        {
            // do nothing
        }

        void IWizard.ProjectFinishedGenerating(Project project)
        {
            ProjectFinishedGenerating(project);
        }

        void IWizard.ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            ProjectItemFinishedGenerating(projectItem);
        }

        void IWizard.RunFinished()
        {
        }

        void IWizard.RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            // alternatively could get body of WizardData element from replacementsDictionary["$wizarddata$"] instead of parsing vstemplate file.
            RunStarted(automationObject, replacementsDictionary, runKind, customParams);
        }

        bool IWizard.ShouldAddProjectItem(string filePath)
        {
            // always add all project items
            return true;
        }

        internal static string DetermineSolutionDirectory(Dictionary<string, string> replacementsDictionary)
        {
            // the $solutiondirectory$ parameter is available in VS11 RC and later
            // No $solutiondirectory$? Ok, we're in the case where the solution is in
            // the same directory as the project
            // Is $specifiedsolutionname$ null or empty? We're definitely in the solution
            // in same directory as project case.

            string solutionName;
            string solutionDir;
            bool ignoreSolutionDir = (replacementsDictionary.TryGetValue("$specifiedsolutionname$", out solutionName) && String.IsNullOrEmpty(solutionName));

            // We check $destinationdirectory$ twice because we want the following precedence:
            // 1. If $specifiedsolutionname$ == null, ALWAYS use $destinationdirectory$
            // 2. Otherwise, use $solutiondirectory$ if available
            // 3. If $solutiondirectory$ is not available, use $destinationdirectory$.
            if ((ignoreSolutionDir && replacementsDictionary.TryGetValue("$destinationdirectory$", out solutionDir)) ||
                replacementsDictionary.TryGetValue("$solutiondirectory$", out solutionDir) ||
                replacementsDictionary.TryGetValue("$destinationdirectory$", out solutionDir))
            {
                return solutionDir;
            }
            return null;
        }

        private enum RepositoryType
        {
            /// <summary>
            /// Cache location relative to the template (inside the same folder as the vstemplate file)
            /// </summary>
            Template,

            /// <summary>
            /// Cache location relative to the VSIX that packages the project template
            /// </summary>
            Extension,

            /// <summary>
            /// Cache location stored in the registry
            /// </summary>
            Registry,
        }
    }
}