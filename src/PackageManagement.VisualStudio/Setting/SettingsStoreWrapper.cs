using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class SettingsStoreWrapper : ISettingsStore
    {
        private readonly IVsSettingsStore _store;

        public SettingsStoreWrapper(IVsSettingsStore store)
        {
            _store = store;
        }

        public bool CollectionExists(string collection)
        {
            int exists;
            int hr = _store.CollectionExists(collection, out exists);
            return ErrorHandler.Succeeded(hr) && exists == 1;
        }

        public bool GetBoolean(string collection, string propertyName, bool defaultValue)
        {
            int value;
            _store.GetBoolOrDefault(collection, propertyName, defaultValue ? 1 : 0, out value);
            return value != 0;
        }

        public int GetInt32(string collection, string propertyName, int defaultValue)
        {
            int value;
            int hr = _store.GetIntOrDefault(collection, propertyName, defaultValue, out value);
            return ErrorHandler.Succeeded(hr) ? value : 0;
        }

        public string GetString(string collection, string propertyName, string defaultValue)
        {
            string value;
            int hr = _store.GetStringOrDefault(collection, propertyName, defaultValue, out value);
            return ErrorHandler.Succeeded(hr) ? value : null;
        }
    }
}
