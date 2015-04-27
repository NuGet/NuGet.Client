using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Packaging.Build
{
    public class Metadata
    {
        private readonly Dictionary<string, object> _metadata = new Dictionary<string, object>();

        public IEnumerable<KeyValuePair<string, object>> GetValues()
        {
            return _metadata;
        }

        public object GetMetadataValue(string name)
        {
            object value;
            if (_metadata.TryGetValue(name, out value))
            {
                return value;
            }
            return null;
        }


        public void SetMetadataValue(string name, object value)
        {
            _metadata[name] = KeepRawValue(value) ? value : value?.ToString();
        }

        public void SetMetadataValue(string name, IEnumerable<string> values)
        {
            _metadata[name] = values;
        }

        private static bool KeepRawValue(object value)
        {
            return value is bool || value is string;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var item in _metadata)
            {
                sb.AppendFormat("{0} = {1}", item.Key, item.Value)
                  .AppendLine();
            }
            return sb.ToString();
        }
    }
}