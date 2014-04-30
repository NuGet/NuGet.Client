using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    internal class Utils
    {
        public static IDictionary<string, object> ObjectToDictionary(object payload)
        {
            return payload.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToDictionary(p => p.Name, p => p.GetValue(payload));
        }
    }
}
