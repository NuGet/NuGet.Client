using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class SettingsManagerWrapper : ISettingsManager
    {
        private readonly IVsSettingsManager _settingsManager;

        public SettingsManagerWrapper(IServiceProvider serviceProvider)
        {
            _settingsManager = (IVsSettingsManager)serviceProvider.GetService(typeof(SVsSettingsManager));
            Debug.Assert(_settingsManager != null);
        }

        public ISettingsStore GetReadOnlySettingsStore()
        {
            IVsSettingsStore settingsStore;
            int hr = _settingsManager.GetReadOnlySettingsStore((uint)__VsSettingsScope.SettingsScope_UserSettings, out settingsStore);
            if (ErrorHandler.Succeeded(hr) && settingsStore != null)
            {
                return new SettingsStoreWrapper(settingsStore);
            }

            return null;
        }

        public IWritableSettingsStore GetWritableSettingsStore()
        {
            IVsWritableSettingsStore settingsStore;

            int hr = _settingsManager.GetWritableSettingsStore((uint)__VsSettingsScope.SettingsScope_UserSettings, out settingsStore);
            if (ErrorHandler.Succeeded(hr) && settingsStore != null)
            {
                return new WritableSettingsStoreWrapper(settingsStore);
            }

            return null;
        }
    }
}
