// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using IStream = Microsoft.VisualStudio.OLE.Interop.IStream;

namespace NuGetVSExtension
{
    /// <summary>
    /// Persists user settings in a solution user options file (*.suo).
    /// </summary>
    [Export]
    [Export(typeof(IUserSettingsManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SolutionUserOptions : IUserSettingsManager, IVsPersistSolutionOpts
    {
        private const string NuGetOptionsStreamKey = "nuget";

        private readonly IServiceProvider _serviceProvider;
        private NuGetSettings _settings = new NuGetSettings();

        [ImportingConstructor]
        public SolutionUserOptions(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        public UserSettings GetSettings(string key)
        {
            UserSettings settings;
            if (_settings.WindowSettings.TryGetValue(key, out settings))
            {
                return settings ?? new UserSettings();
            }

            return new UserSettings();
        }

        public void AddSettings(string key, UserSettings obj)
        {
            _settings.WindowSettings[key] = obj;
        }

        public void ApplyShowDeprecatedFrameworkSetting(bool show)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = _serviceProvider.GetService<SVsUIShell,IVsUIShell>();
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                packageManagerControl?.ApplyShowDeprecatedFrameworkSetting(show);
            }
        }

        public void ApplyShowPreviewSetting(bool show)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                packageManagerControl?.ApplyShowPreviewSetting(show);
            }
        }

        public bool LoadSettings()
        {
            var solutionPersistence = Package.GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            if (solutionPersistence.LoadPackageUserOpts(this, NuGetOptionsStreamKey) != VSConstants.S_OK)
            {
                return false;
            }

            return true;
        }

        public bool PersistSettings()
        {
            var solutionPersistence = Package.GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            if (solutionPersistence.SavePackageUserOpts(this, NuGetOptionsStreamKey) != VSConstants.S_OK)
            {
                return false;
            }

            return true;
        }

        #region IVsPersistSolutionOpts

        // Called by the shell when a solution is opened and the SUO file is read.
        public int LoadUserOptions(IVsSolutionPersistence pPersistence, uint grfLoadOpts)
        {
            pPersistence.LoadPackageUserOpts(this, NuGetOptionsStreamKey);
            return VSConstants.S_OK;
        }

        // Called by the shell if the _strSolutionUserOptionsKey section declared in LoadUserOptions() as 
        // being written by this package has been found in the suo file
        public int ReadUserOptions(IStream pOptionsStream, string _)
        {
            _settings = new NuGetSettings();

            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    var serializer = new BinaryFormatter();
                    var obj = serializer.Deserialize(stream) as NuGetSettings;
                    if (obj != null)
                    {
                        _settings = obj;
                    }
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }

        // Called by the shell when the SUO file is saved. The provider calls the shell back to let it 
        // know which options keys it will use in the suo file.
        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            pPersistence.SavePackageUserOpts(this, NuGetOptionsStreamKey);
            return VSConstants.S_OK;
        }

        // Called by the shell to let the package write user options under the specified key.
        public int WriteUserOptions(IStream pOptionsStream, string _)
        {
            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    var serializer = new BinaryFormatter();
                    serializer.Serialize(stream, _settings);
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }

        #endregion IVsPersistSolutionOpts
    }
}
