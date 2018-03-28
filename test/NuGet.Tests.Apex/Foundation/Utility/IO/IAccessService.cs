using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NuGet.Tests.Foundation.Utility.IO
{
    public interface IAccessService
    {
        #region File APIs, grouped by purpose

        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        string[] SafeAppContainerPaths { get; }

        // Get a FileStream

        FileStream FileCreate(string path);
        FileStream FileCreate(string path, int bufferSize);
        FileStream FileCreate(string path, int bufferSize, FileOptions options);
        FileStream FileCreate(string path, int bufferSize, FileOptions options, FileSecurity fileSecurity);
        FileStream FileOpen(string path, FileMode mode);
        FileStream FileOpen(string path, FileMode mode, FileAccess access);
        FileStream FileOpen(string path, FileMode mode, FileAccess access, FileShare share, bool forceLowBoxPermissions = false);
        FileStream FileOpenRead(string path);
        Stream FileOpenWrite(string path);

        // Simple read/write to a hidden FileStream

        void FileAppendAllLines(string path, IEnumerable<string> contents);
        void FileAppendAllLines(string path, IEnumerable<string> contents, Encoding encoding);
        void FileAppendAllText(string path, string contents);
        void FileAppendAllText(string path, string contents, Encoding encoding);
        StreamWriter FileAppendText(string path);
        StreamWriter FileCreateText(string path);
        StreamReader FileOpenText(string path);
        byte[] FileReadBytes(string path, int count);
        byte[] FileReadAllBytes(string path);
        string[] FileReadAllLines(string path);
        string[] FileReadAllLines(string path, Encoding encoding);
        string FileReadAllText(string path);
        string FileReadAllText(string path, Encoding encoding);
        IEnumerable<string> FileReadLines(string path);
        IEnumerable<string> FileReadLines(string path, Encoding encoding);
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames")] // this matches existing File method signature
        void FileWriteAllBytes(string path, byte[] bytes);
        void FileWriteAllLines(string path, IEnumerable<string> contents);
        void FileWriteAllLines(string path, IEnumerable<string> contents, Encoding encoding);
        void FileWriteAllText(string path, string contents);
        void FileWriteAllText(string path, string contents, Encoding encoding);

        // Windows Explorer stuff

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")] // this matches existing File method signature
        void FileCopy(string sourceFileName, string destFileName);
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")] // this matches existing File method signature
        void FileCopy(string sourceFileName, string destFileName, bool overwrite);
        void FileDecrypt(string path);
        void FileDelete(string path);
        void FileEncrypt(string path);
        bool FileExists(string path);
        FileSecurity FileGetAccessControl(string path);
        FileSecurity FileGetAccessControl(string path, AccessControlSections includeSections);
        FileAttributes FileGetAttributes(string path);
        DateTime FileGetCreationTime(string path);
        DateTime FileGetLastAccessTime(string path);
        DateTime FileGetLastWriteTime(string path);
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")] // this matches existing File method signature
        void FileMove(string sourceFileName, string destFileName);
        void FileReplace(string sourceFileName, string destinationFileName, string destinationBackupFileName);
        void FileReplace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);
        void FileSetAccessControl(string path, FileSecurity fileSecurity);
        void FileSetAttributes(string path, FileAttributes fileAttributes);
        void FileSetCreationTime(string path, DateTime creationTime);
        void FileSetLastAccessTime(string path, DateTime lastAccessTime);
        void FileSetLastWriteTime(string path, DateTime lastWriteTime);

        // UTC copies of methods above

        DateTime FileGetCreationTimeUtc(string path);
        DateTime FileGetLastAccessTimeUtc(string path);
        DateTime FileGetLastWriteTimeUtc(string path);
        void FileSetCreationTimeUtc(string path, DateTime creationTimeUtc);
        void FileSetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
        void FileSetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);

        #endregion

        #region Directory APIs, grouped by purpose

        // Windows Explorer stuff

        DirectoryInfo DirectoryCreateDirectory(string path);
        DirectoryInfo DirectoryCreateDirectory(string path, DirectorySecurity directorySecurity);
        void DirectoryDelete(string path);
        void DirectoryDelete(string path, bool recursive);
        IEnumerable<string> DirectoryEnumerateDirectories(string path);
        IEnumerable<string> DirectoryEnumerateDirectories(string path, string searchPattern);
        IEnumerable<string> DirectoryEnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
        IEnumerable<string> DirectoryEnumerateFiles(string path);
        IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern);
        IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern, SearchOption searchOption);
        IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path);
        IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path, string searchPattern);
        IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
        bool DirectoryExists(string path);
        DirectorySecurity DirectoryGetAccessControl(string path);
        DirectorySecurity DirectoryGetAccessControl(string path, AccessControlSections includeSections);
        DateTime DirectoryGetCreationTime(string path);
        string DirectoryGetCurrentDirectory();
        string[] DirectoryGetDirectories(string path);
        string[] DirectoryGetDirectories(string path, string searchPattern);
        string[] DirectoryGetDirectories(string path, string searchPattern, SearchOption searchOption);
        string DirectoryGetDirectoryRoot(string path);
        string[] DirectoryGetFiles(string path);
        string[] DirectoryGetFiles(string path, string searchPattern);
        string[] DirectoryGetFiles(string path, string searchPattern, SearchOption searchOption);
        string[] DirectoryGetFileSystemEntries(string path);
        string[] DirectoryGetFileSystemEntries(string path, string searchPattern);
        string[] DirectoryGetFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
        DateTime DirectoryGetLastAccessTime(string path);
        DateTime DirectoryGetLastWriteTime(string path);
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")] // this matches existing Directory method signature
        void DirectoryMove(string sourceDirName, string destDirName);
        void DirectorySetAccessControl(string path, DirectorySecurity directorySecurity);
        void DirectorySetCreationTime(string path, DateTime creationTime);
        void DirectorySetCurrentDirectory(string path);
        void DirectorySetLastAccessTime(string path, DateTime lastAccessTime);
        void DirectorySetLastWriteTime(string path, DateTime lastWriteTime);

        // UTC copies of methods above

        DateTime DirectoryGetCreationTimeUtc(string path);
        DateTime DirectoryGetLastAccessTimeUtc(string path);
        DateTime DirectoryGetLastWriteTimeUtc(string path);
        void DirectorySetCreationTimeUtc(string path, DateTime creationTimeUtc);
        void DirectorySetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
        void DirectorySetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);

        #endregion

        #region Registry access

        object RegistryGetValue(string keyName, string valueName, object defaultValue);
        void RegistrySetValue(string keyName, string valueName, object value);
        void RegistrySetValue(string keyName, string valueName, object value, RegistryValueKind valueKind);

        #endregion

        #region Miscellaneous
        string MiscGetFullPathName(string path);
        string MiscGetTempFileName();
        string MiscGetUserSharedTempPath();
        bool MiscCanLoadAssembly(string path);
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#")]
        bool MiscGetFileAttributes(string path, out FileAttributes attributes);
        bool MiscPathExists(string path);
        bool MiscDirectoryExists(string path);
        bool MiscFileExists(string path);
        bool MiscSetFileAttributes(string path, FileAttributes attributes);
        void MiscConvertExeToDll(string exeAsDllPath);

        void LaunchNotepad(string arguments);
        bool SetCursorPos(Point position);

        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "WebPage")]
        void LaunchWebPage(Uri uri);

        AssemblyName GetAssemblyNameFromPath(string path);
        Version GetReferencedVersion(string assemblyPath, string referencedAssemblyName);

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings")]
        XmlNodeName GetXmlNode(string path, string nodeName, string namespaceUri);
        #endregion

        #region FileDataCache helpers

        void CacheSafeDeleteFile(string cacheFile);
        void CacheDeleteMatchingFiles(string path, string fileSpec);
        void CacheDeleteOldMatchingFiles(string path, string fileSpec, DateTime staleUtcTime);
        bool CacheIsValid(DateTime sourceFileTimestamp, string cacheFile, double timestampTolerance);

        byte[] CacheReadCacheFile(DateTime sourceFileTimestamp, string cacheFile, double timestampTolerance);
        bool CacheWriteCacheFile(DateTime sourceFileTimestamp, string cacheFile, byte[] fileData);

        #endregion

        Version EnvironmentOSVersion();
    }
}
