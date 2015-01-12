using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public static class StreamUtility
    {
        public static Stream StreamFromString(string content)
        {
            return StreamFromString(content, Encoding.UTF8);
        }

        public static Stream StreamFromString(string content, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(content));
        }
    }
}
