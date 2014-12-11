using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public static class JsonLdExtensions
    {
        
        /// <summary>
        /// True if GetValue() matches
        /// </summary>
        public static bool IsValue(this JsonLD.Core.RDFDataset.Node node, string value)
        {
            return node != null && StringComparer.Ordinal.Equals(node.GetValue(), value);
        }
    }
}
