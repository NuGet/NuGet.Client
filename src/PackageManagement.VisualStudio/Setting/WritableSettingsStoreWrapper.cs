using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class WritableSettingsStoreWrapper : SettingsStoreWrapper, IWritableSettingsStore
    {
        private readonly IVsWritableSettingsStore _store;

        public WritableSettingsStoreWrapper(IVsWritableSettingsStore store)
            : base(store)
        {
            _store = store;
        }

        public void DeleteCollection(string collection)
        {
            _store.DeleteCollection(collection);
        }

        public void CreateCollection(string collection)
        {
            _store.CreateCollection(collection);
        }

        public bool DeleteProperty(string collection, string propertyName)
        {
            return ErrorHandler.Succeeded(_store.DeleteProperty(collection, propertyName));
        }

        public void SetBoolean(string collection, string propertyName, bool value)
        {
            _store.SetBool(collection, propertyName, value ? 1 : 0);
        }

        public void SetInt32(string collection, string propertyName, int value)
        {
            _store.SetInt(collection, propertyName, value);
        }

        public void SetString(string collection, string propertyName, string value)
        {
            _store.SetString(collection, propertyName, value);
        }
    }
}
