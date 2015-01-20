using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface ISettingsStore
    {
        bool CollectionExists(string collection);
        bool GetBoolean(string collection, string propertyName, bool defaultValue);
        int GetInt32(string collection, string propertyName, int defaultValue);
        string GetString(string collection, string propertyName, string defaultValue);
    }
}
