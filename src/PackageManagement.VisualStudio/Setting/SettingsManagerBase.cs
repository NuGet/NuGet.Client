using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    public abstract class SettingsManagerBase
    {
        private readonly ISettingsManager _settingsManager;

        protected SettingsManagerBase(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            _settingsManager = new SettingsManagerWrapper(serviceProvider);
        }

        protected bool ReadBool(string settingsRoot, string property, bool defaultValue = false)
        {
            var userSettingsStore = _settingsManager.GetReadOnlySettingsStore();
            if (userSettingsStore != null && userSettingsStore.CollectionExists(settingsRoot))
            {
                return userSettingsStore.GetBoolean(settingsRoot, property, defaultValue);
            }
            else
            {
                return defaultValue;
            }
        }

        protected void WriteBool(string settingsRoot, string property, bool value)
        {
            IWritableSettingsStore userSettingsStore = GetWritableSettingsStore(settingsRoot);
            if (userSettingsStore != null)
            {
                userSettingsStore.SetBoolean(settingsRoot, property, value);
            }
        }

        protected int ReadInt32(string settingsRoot, string property, int defaultValue = 0)
        {
            var userSettingsStore = _settingsManager.GetReadOnlySettingsStore();
            if (userSettingsStore != null && userSettingsStore.CollectionExists(settingsRoot))
            {
                return userSettingsStore.GetInt32(settingsRoot, property, defaultValue);
            }
            else
            {
                return defaultValue;
            }
        }

        protected void WriteInt32(string settingsRoot, string property, int value)
        {
            IWritableSettingsStore userSettingsStore = GetWritableSettingsStore(settingsRoot);
            if (userSettingsStore != null)
            {
                userSettingsStore.SetInt32(settingsRoot, property, value);
            }
        }

        protected string ReadString(string settingsRoot, string property, string defaultValue = "")
        {
            var userSettingsStore = _settingsManager.GetReadOnlySettingsStore();
            if (userSettingsStore != null && userSettingsStore.CollectionExists(settingsRoot))
            {
                return userSettingsStore.GetString(settingsRoot, property, defaultValue);
            }
            else
            {
                return defaultValue;
            }
        }

        protected string[] ReadStrings(string settingsRoot, string[] properties, string defaultValue = "")
        {
            var userSettingsStore = _settingsManager.GetReadOnlySettingsStore();
            if (userSettingsStore != null && userSettingsStore.CollectionExists(settingsRoot))
            {
                string[] values = new string[properties.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = userSettingsStore.GetString(settingsRoot, properties[i], defaultValue);
                }
                return values;
            }
            else
            {
                return null;
            }
        }

        protected bool DeleteProperty(string settingsRoot, string property)
        {
            IWritableSettingsStore userSettingsStore = GetWritableSettingsStore(settingsRoot);
            if (userSettingsStore != null)
            {
                return userSettingsStore.DeleteProperty(settingsRoot, property);
            }
            else
            {
                return false;
            }
        }

        protected void WriteStrings(string settingsRoot, string[] properties, string[] values)
        {
            Debug.Assert(properties.Length == values.Length);

            IWritableSettingsStore userSettingsStore = GetWritableSettingsStore(settingsRoot);
            if (userSettingsStore != null)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    userSettingsStore.SetString(settingsRoot, properties[i], values[i]);
                }
            }
        }

        protected void WriteString(string settingsRoot, string property, string value)
        {
            IWritableSettingsStore userSettingsStore = GetWritableSettingsStore(settingsRoot);
            if (userSettingsStore != null)
            {
                userSettingsStore.SetString(settingsRoot, property, value);
            }
        }

        protected void ClearAllSettings(string settingsRoot)
        {
            IWritableSettingsStore userSettingsStore = _settingsManager.GetWritableSettingsStore();
            if (userSettingsStore != null && userSettingsStore.CollectionExists(settingsRoot))
            {
                userSettingsStore.DeleteCollection(settingsRoot);
            }
        }

        private IWritableSettingsStore GetWritableSettingsStore(string settingsRoot)
        {
            IWritableSettingsStore userSettingsStore = _settingsManager.GetWritableSettingsStore();
            if (userSettingsStore != null && !userSettingsStore.CollectionExists(settingsRoot))
            {
                userSettingsStore.CreateCollection(settingsRoot);
            }
            return userSettingsStore;
        }
    }
}
