using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Commands;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// TaskItem wrapper
    /// </summary>
    public class MSBuildTaskItem : IMSBuildItem
    {
        public ITaskItem Item { get; }

        public string Identity
        {
            get
            {
                return Item.ItemSpec;
            }
        }

        public IReadOnlyList<string> Properties
        {
            get
            {
                return Item.MetadataNames.OfType<string>().ToList();
            }
        }

        public string GetProperty(string property)
        {
            try
            {
                var val = Item.GetMetadata(property);

                if (!string.IsNullOrEmpty(val))
                {
                    // Ignore empty strings
                    return val;
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        public MSBuildTaskItem(ITaskItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Item = item;
        }

        public override string ToString()
        {
            return $"Type: {GetProperty("Type")} Project: {GetProperty("ProjectUniqueName")}";
        }
    }
}
