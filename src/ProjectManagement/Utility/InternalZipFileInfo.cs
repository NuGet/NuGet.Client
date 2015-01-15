using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public class InternalZipFileInfo
    {
        public string ZipArchivePath { get; private set;}
        public string ZipArchiveEntryFullName { get; private set;}
        public InternalZipFileInfo(string zipArchivePath, string zipArchiveEntryFullName)
        {
            ZipArchivePath = zipArchivePath;
            ZipArchiveEntryFullName = zipArchiveEntryFullName;
        }
    }
}
