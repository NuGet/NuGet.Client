using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public interface IFileSystem
    {
        IEnumerable<string> GetFiles();

        Stream OpenFile(string path);

        IEnumerable<string> GetFolders(string path);
    }
}
