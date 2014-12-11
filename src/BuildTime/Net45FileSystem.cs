using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class Net45FileSystem : IFileSystem
    {
        private readonly DirectoryInfo _root;

        public Net45FileSystem(string folderPath)
        {
            _root = new DirectoryInfo(folderPath);
        }

        public IEnumerable<string> GetFiles()
        {
            return _root.GetFiles("*", SearchOption.AllDirectories).Select(f => f.FullName.Replace(_root.FullName, string.Empty));
        }

        public Stream OpenFile(string path)
        {
            Stream stream = null;
            FileInfo file = new FileInfo(Path.Combine(_root.FullName, path));

            // make sure the path 
            if (file.Exists && file.FullName.StartsWith(_root.FullName))
            {
                stream = file.OpenRead();
            }

            return stream;
        }

        public IEnumerable<string> GetFolders(string path)
        {
            IEnumerable<string> dirs = Enumerable.Empty<string>();

            DirectoryInfo dir = new DirectoryInfo(Path.Combine(_root.FullName, path));

            if (dir.Exists && dir.FullName.StartsWith(_root.FullName))
            {
                dirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly).Select(d => d.FullName.Replace(_root.FullName, string.Empty));
            }

            return dirs;
        }
    }
}
