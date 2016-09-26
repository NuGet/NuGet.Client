using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace NuGet.Build.Tasks
{
    public static class BuildTasksUtility
    {
        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key)
        {
            CopyPropertyIfExists(item, properties, key, key);
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key, string toKey)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue)
                && !properties.ContainsKey(key))
            {
                properties.Add(toKey, propertyValue);
            }
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)
                && !properties.ContainsKey(key))
            {
                properties.Add(key, value);
            }
        }
    }
}