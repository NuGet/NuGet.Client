using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Commands
{
    /// <summary>
    /// Internal ITaskItem abstraction
    /// </summary>
    public class MSBuildItem : IMSBuildItem
    {
        private readonly IDictionary<string, string> _metadata;

        public string Identity { get; }

        public IReadOnlyList<string> Properties
        {
            get
            {
                return _metadata.Keys.ToList();
            }
        }

        public MSBuildItem(string identity, IDictionary<string, string> metadata)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            Identity = identity;
            _metadata = metadata;
        }

        /// <summary>
        /// Get property or null if empty.
        /// </summary>
        public string GetProperty(string key)
        {
            string val;
            if (_metadata.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }

            return null;
        }

        public override string ToString()
        {
            return $"Type: {GetProperty("Type")} Project: {GetProperty("ProjectUniqueName")}";
        }
    }
}
