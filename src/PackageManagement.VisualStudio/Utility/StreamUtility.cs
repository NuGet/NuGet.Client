using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio.Utility
{
    public static class StreamUtility
    {
        public static Stream AsStream(string value)
        {
            return AsStream(value, Encoding.UTF8);
        }

        public static Stream AsStream(string value, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(value));
        }
    }
}
