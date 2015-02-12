using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.VisualStudio.Resources;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.PackageManagement;

namespace NuGet.VisualStudio
{
    [Export(typeof(IRepositorySettings))]
    public class RepositorySettings : IRepositorySettings
    {
        internal const string DefaultRepositoryDirectory = "packages";
        private const string NuGetConfig = "nuget.config";

        private string _configurationPath;
        //private IFileSystem _fileSystem;
        private ISettings _settings;
        private readonly ISolutionManager _solutionManager;
        //private readonly IFileSystemProvider _fileSystemProvider;
        private readonly IMachineWideSettings _machineWideSettings;
        
        [ImportingConstructor]
        public RepositorySettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            //if (fileSystemProvider == null)
            //{
            //    throw new ArgumentNullException("fileSystemProvider");
            //}

            //if (sourceControlTracker == null)
            //{
            //    throw new ArgumentNullException("sourceControlTracker");
            //}

            _solutionManager = solutionManager;
            //_fileSystemProvider = fileSystemProvider;
            _settings = null;
            _machineWideSettings = machineWideSettings;

            EventHandler resetConfiguration = (sender, e) =>
            {
                // Kill our configuration cache when someone closes the solution
                _configurationPath = null;
                _settings = null;
            };

            _solutionManager.SolutionClosing += resetConfiguration;
            //sourceControlTracker.SolutionBoundToSourceControl += resetConfiguration;
        }

        internal RepositorySettings(
            ISolutionManager solutionManager) : 
            this(solutionManager, machineWideSettings: null)
        {
        }

        public string RepositoryPath
        {
            get
            {
                return GetRepositoryPath();
            }
        }

        public string ConfigFolderPath
        {
            get
            {
                return GetConfigFolderPath();
            }
        }

        private string GetConfigFolderPath()
        {
            if (String.IsNullOrEmpty(_solutionManager.SolutionDirectory))
            {
                throw new InvalidOperationException(VsResources.SolutionDirectoryNotAvailable);
            }

            return Path.Combine(_solutionManager.SolutionDirectory, NuGetVSConstants.NuGetSolutionSettingsFolder);
        }

        //private IFileSystem FileSystem
        //{
        //    get
        //    {
        //        if (_fileSystem == null)
        //        {
        //            string configFolderPath = GetConfigFolderPath();
        //            _fileSystem = _fileSystemProvider.GetFileSystem(configFolderPath);
        //        }
        //        return _fileSystem;
        //    }
        //}

        private ISettings DefaultSettings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = Settings.LoadDefaultSettings(
                        _solutionManager.SolutionDirectory, 
                        configFileName: null,
                        machineWideSettings: _machineWideSettings);
                }
                return _settings;
            }
        }

        private string GetRepositoryPath()
        {
            // If the solution directory is unavailable then throw an exception
            if (String.IsNullOrEmpty(_solutionManager.SolutionDirectory))
            {
                throw new InvalidOperationException(VsResources.SolutionDirectoryNotAvailable);
            }

            // Get the configuration path (if any)
            string configurationPath = GetConfigurationPath();

            string path = null;
            string directoryPath = _solutionManager.SolutionDirectory;

            // If we found a config file, try to read it
            if (!String.IsNullOrEmpty(configurationPath))
            {
                // Read the path from the file
                path = GetRepositoryPathFromConfig(configurationPath);
            }

            if (String.IsNullOrEmpty(path))
            {
                // If the path is null, look in default settings
                path = DefaultSettings.Root;

                if (String.IsNullOrEmpty(path))
                {
                    // if default settings file does not provide a path either, then default to the directory
                    path = DefaultRepositoryDirectory;
                }
                else
                {
                    return Path.GetFullPath(path);
                }
            }
            else
            {
                // Resolve the path relative to the configuration path
                directoryPath = Path.GetDirectoryName(configurationPath);
            }

            return Path.Combine(directoryPath, path);
        }

        /// <summary>
        /// Returns the configuraton path by walking the directory structure to find a nuget.config file.
        /// </summary>
        private string GetConfigurationPath()
        {
            if (CheckConfiguration())
            {
                // Start from the solution directory and try to find a nuget.config in the list of candidates
                _configurationPath = (from directory in GetConfigurationDirectories(_solutionManager.SolutionDirectory)
                                      let configPath = Path.Combine(directory, NuGetConfig)
                                      where File.Exists(configPath)
                                      select configPath).FirstOrDefault();
            }

            return _configurationPath;
        }

        private bool CheckConfiguration()
        {
            // If there's no saved configuration path then look for a configuration file.
            // This is to accommodate the workflow where someone changes the solution repository
            // after installing packages using the default "packages" folder.

            // REVIEW: Do we always look even in the default scenario where the user has no nuget.config file?
            if (String.IsNullOrEmpty(_configurationPath))
            {
                return true;
            }

            // If we have a configuration file path cached. We only do the directory walk if the file no longer exists
            return !File.Exists(_configurationPath);
        }

        /// <summary>
        /// Extracts the repository path from a nuget.config settings file
        /// </summary>
        /// <param name="path">Full path to the nuget.config file</param>
        private string GetRepositoryPathFromConfig(string path)
        {
            try
            {
                XDocument document;
                using (Stream stream = File.OpenRead(path))
                {
                    document = XmlUtility.LoadSafe(stream);
                }

                // <settings>
                //    <repositoryPath>..</repositoryPath>
                // </settings>
                string repositoryPath = document.Root.GetOptionalElementValue("repositoryPath");
                if (!String.IsNullOrEmpty(repositoryPath))
                {
                    repositoryPath = repositoryPath.Replace('/', Path.DirectorySeparatorChar);
                }
                return repositoryPath;
            }
            catch (XmlException e)
            {
                // Set the configuration path to null if it fails
                _configurationPath = null;

                // If we were unable to parse the configuration file then show an error
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                                  VsResources.ErrorReadingFile, path), e);
            }
        }

        /// <summary>
        /// Returns the list of candidates for nuget config files.
        /// </summary>
        private IEnumerable<string> GetConfigurationDirectories(string path)
        {
            // look for nuget.config under '<solution root>\.nuget' folder first
            yield return Path.Combine(path, NuGetVSConstants.NuGetSolutionSettingsFolder);

            while (!String.IsNullOrEmpty(path))
            {
                yield return path;

                path = Path.GetDirectoryName(path);
            }
        }
    }
}