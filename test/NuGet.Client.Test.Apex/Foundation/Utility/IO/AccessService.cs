using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using System.Xml;
using System.Globalization;
using System.Runtime.InteropServices;
using NuGetClient.Test.Foundation.Utility.Assemblies;
using NuGetClient.Test.Foundation.Utility.Data;

namespace NuGetClient.Test.Foundation.Utility.IO
{
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "FileChangeWatcherService has it's life-cycle controlled by HostServiceProvider.")]
    internal sealed class AccessService : IAccessService
    {
        private bool isWin8 = false;
        private static byte[] EmptyFileBytes = new byte[0];
        private HashSet<string> safeAppContainerPaths = new HashSet<string>();
        private IFileChangeWatcherService fileChangeWatcherService;

        internal AccessService(bool isWin8)
        {
            this.isWin8 = isWin8;
        }

        internal void AddSafeAppContainerPath(string path)
        {
            string pathWithSeparator = PathHelper.EnsurePathEndsInDirectorySeparator(path);
            Diagnostics.DebugHelper.Assert(pathWithSeparator != null, "Repro for 213855: Why is pathWithSeparator null?");
            if (pathWithSeparator != null)
            {
                this.safeAppContainerPaths.Add(pathWithSeparator);
            }
        }

        #region public File APIs, grouped by purpose

        public string[] SafeAppContainerPaths
        {
            get { return this.safeAppContainerPaths.ToArray(); }
        }

        // Get a FileStream

        public FileStream FileCreate(string path)
        {
            FileStream result = File.Create(path);
            this.AddFileLowBoxPermissionsIfNecessary(path, FileMode.Create);
            return result;
        }

        public FileStream FileCreate(string path, int bufferSize)
        {
            FileStream result = File.Create(path, bufferSize);
            this.AddFileLowBoxPermissionsIfNecessary(path, FileMode.Create);
            return result;
        }

        public FileStream FileCreate(string path, int bufferSize, FileOptions options)
        {
            FileStream result = File.Create(path, bufferSize, options);
            this.AddFileLowBoxPermissionsIfNecessary(path, FileMode.Create);
            return result;
        }

        public FileStream FileCreate(string path, int bufferSize, FileOptions options, FileSecurity fileSecurity)
        {
            FileStream result = File.Create(path, bufferSize, options, fileSecurity);
            this.AddFileLowBoxPermissionsIfNecessary(path, FileMode.Create);
            return result;
        }

        public FileStream FileOpen(string path, FileMode mode)
        {
            FileStream result = File.Open(path, mode);
            this.AddFileLowBoxPermissionsIfNecessary(path, mode);
            return result;
        }

        public FileStream FileOpen(string path, FileMode mode, FileAccess access)
        {
            FileStream result = File.Open(path, mode, access);
            this.AddFileLowBoxPermissionsIfNecessary(path, mode);
            return result;
        }

        public FileStream FileOpen(string path, FileMode mode, FileAccess access, FileShare share, bool forceLowBoxPermissions = false)
        {
            FileStream result = File.Open(path, mode, access, share);
            this.AddFileLowBoxPermissionsIfNecessary(path, mode, forceLowBoxPermissions: forceLowBoxPermissions);
            return result;
        }

        public FileStream FileOpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public Stream FileOpenWrite(string path)
        {
            return File.OpenWrite(path);
        }

        public void FileAppendAllLines(string path, IEnumerable<string> contents)
        {
            File.AppendAllLines(path, contents);
        }

        public void FileAppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            File.AppendAllLines(path, contents, encoding);
        }

        public void FileAppendAllText(string path, string contents)
        {
            File.AppendAllText(path, contents);
        }

        public void FileAppendAllText(string path, string contents, Encoding encoding)
        {
            File.AppendAllText(path, contents, encoding);
        }

        public StreamWriter FileAppendText(string path)
        {
            return File.AppendText(path);
        }

        public StreamWriter FileCreateText(string path)
        {
            return File.CreateText(path);
        }

        public StreamReader FileOpenText(string path)
        {
            return File.OpenText(path);
        }

        public byte[] FileReadBytes(string path, int count)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                return reader.ReadBytes(count);
            }
        }

        public byte[] FileReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public string[] FileReadAllLines(string path)
        {
            return File.ReadAllLines(path);
        }

        public string[] FileReadAllLines(string path, Encoding encoding)
        {
            return File.ReadAllLines(path, encoding);
        }

        public string FileReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public string FileReadAllText(string path, Encoding encoding)
        {
            return File.ReadAllText(path, encoding);
        }

        public IEnumerable<string> FileReadLines(string path)
        {
            return File.ReadLines(path);
        }

        public IEnumerable<string> FileReadLines(string path, Encoding encoding)
        {
            return File.ReadLines(path, encoding);
        }

        public void FileWriteAllBytes(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        public void FileWriteAllLines(string path, IEnumerable<string> contents)
        {
            File.WriteAllLines(path, contents);
        }

        public void FileWriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            File.WriteAllLines(path, contents, encoding);
        }

        public void FileWriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public void FileWriteAllText(string path, string contents, Encoding encoding)
        {
            File.WriteAllText(path, contents, encoding);
        }

        // Windows Explorer stuff

        public void FileCopy(string sourceFileName, string destFileName)
        {
            File.Copy(sourceFileName, destFileName);
        }

        public void FileCopy(string sourceFileName, string destFileName, bool overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite);
        }

        public void FileDecrypt(string path)
        {
            File.Decrypt(path);
        }

        public void FileDelete(string path)
        {
            File.Delete(path);
        }

        public void FileEncrypt(string path)
        {
            File.Encrypt(path);
        }

        public bool FileExists(string path)
        {
            return PathHelper.FileExists(path);
        }

        public FileSecurity FileGetAccessControl(string path)
        {
            return File.GetAccessControl(path);
        }

        public FileSecurity FileGetAccessControl(string path, AccessControlSections includeSections)
        {
            return File.GetAccessControl(path, includeSections);
        }

        public FileAttributes FileGetAttributes(string path)
        {
            // We can't use Path.GetAttributes as it does not fail on access denied
            FileAttributes attributes;
            PathHelper.GetPathAttributes(path, out attributes, useAccessService: false);
            return attributes;
        }

        public DateTime FileGetCreationTime(string path)
        {
            return File.GetCreationTime(path);
        }

        public DateTime FileGetLastAccessTime(string path)
        {
            return File.GetLastAccessTime(path);
        }

        public DateTime FileGetLastWriteTime(string path)
        {
            return File.GetLastWriteTime(path);
        }

        public void FileMove(string sourceFileName, string destFileName)
        {
            File.Move(sourceFileName, destFileName);
        }

        public void FileReplace(string sourceFileName, string destinationFileName, string destinationBackupFileName)
        {
            File.Replace(sourceFileName, destinationFileName, destinationBackupFileName);
        }

        public void FileReplace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors)
        {
            File.Replace(sourceFileName, destinationFileName, destinationBackupFileName, ignoreMetadataErrors);
        }

        public void FileSetAccessControl(string path, FileSecurity fileSecurity)
        {
            File.SetAccessControl(path, fileSecurity);
        }

        public void FileSetAttributes(string path, FileAttributes fileAttributes)
        {
            File.SetAttributes(path, fileAttributes);
        }

        public void FileSetCreationTime(string path, DateTime creationTime)
        {
            File.SetCreationTime(path, creationTime);
        }

        public void FileSetLastAccessTime(string path, DateTime lastAccessTime)
        {
            File.SetLastAccessTime(path, lastAccessTime);
        }

        public void FileSetLastWriteTime(string path, DateTime lastWriteTime)
        {
            File.SetLastWriteTime(path, lastWriteTime);
        }

        // UTC copies of methods above

        public DateTime FileGetCreationTimeUtc(string path)
        {
            return File.GetCreationTimeUtc(path);
        }

        public DateTime FileGetLastAccessTimeUtc(string path)
        {
            return File.GetLastAccessTimeUtc(path);
        }

        public DateTime FileGetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(path);
        }

        public void FileSetCreationTimeUtc(string path, DateTime creationTimeUtc)
        {
            File.SetCreationTimeUtc(path, creationTimeUtc);
        }

        public void FileSetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
        {
            File.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
        }

        public void FileSetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
        {
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        #endregion

        #region public Directory APIs, grouped by purpose

        // Windows Explorer stuff

        public DirectoryInfo DirectoryCreateDirectory(string path)
        {
            DirectoryInfo result = Directory.CreateDirectory(path);
            this.AddDirectoryLowBoxPermissionsIfNecessary(path);
            return result;
        }

        public DirectoryInfo DirectoryCreateDirectory(string path, DirectorySecurity directorySecurity)
        {
            DirectoryInfo result = Directory.CreateDirectory(path, directorySecurity);
            this.AddDirectoryLowBoxPermissionsIfNecessary(path);
            return result;
        }

        public void DirectoryDelete(string path)
        {
            this.StopWatchingDirectory(path);
            Directory.Delete(path);
        }

        public void DirectoryDelete(string path, bool recursive)
        {
            this.StopWatchingDirectory(path);
            Directory.Delete(path, recursive);
        }

        private void StopWatchingDirectory(string path)
        {
            if (this.fileChangeWatcherService != null)
            {
                this.fileChangeWatcherService.StopWatchingDirectory(path);
            }
#if DEBUG
            else
            {
                Debug.Assert(UnitTestHelper.IsUnitTestEnvironment, "We shouldn't be deleting a directory before FileChangeWatcherService is initialized");
            }
#endif
        }

        public IEnumerable<string> DirectoryEnumerateDirectories(string path)
        {
            return Directory.EnumerateDirectories(path);
        }

        public IEnumerable<string> DirectoryEnumerateDirectories(string path, string searchPattern)
        {
            return Directory.EnumerateDirectories(path, searchPattern);
        }

        public IEnumerable<string> DirectoryEnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public IEnumerable<string> DirectoryEnumerateFiles(string path)
        {
            return Directory.EnumerateFiles(path);
        }

        public IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern)
        {
            return Directory.EnumerateFiles(path, searchPattern);
        }

        public IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path)
        {
            return Directory.EnumerateFileSystemEntries(path);
        }

        public IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path, string searchPattern)
        {
            return Directory.EnumerateFileSystemEntries(path, searchPattern);
        }

        public IEnumerable<string> DirectoryEnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        public bool DirectoryExists(string path)
        {
            return PathHelper.DirectoryExists(path);
        }

        public DirectorySecurity DirectoryGetAccessControl(string path)
        {
            return Directory.GetAccessControl(path);
        }

        public DirectorySecurity DirectoryGetAccessControl(string path, AccessControlSections includeSections)
        {
            return Directory.GetAccessControl(path, includeSections);
        }

        public DateTime DirectoryGetCreationTime(string path)
        {
            return Directory.GetCreationTime(path);
        }

        public string DirectoryGetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public string[] DirectoryGetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        public string[] DirectoryGetDirectories(string path, string searchPattern)
        {
            return Directory.GetDirectories(path, searchPattern);
        }

        public string[] DirectoryGetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.GetDirectories(path, searchPattern, searchOption);
        }

        public string DirectoryGetDirectoryRoot(string path)
        {
            return Directory.GetDirectoryRoot(path);
        }

        public string[] DirectoryGetFiles(string path)
        {
            return Directory.GetFiles(path);
        }

        public string[] DirectoryGetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern);
        }

        public string[] DirectoryGetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }

        public string[] DirectoryGetFileSystemEntries(string path)
        {
            return Directory.GetFileSystemEntries(path);
        }

        public string[] DirectoryGetFileSystemEntries(string path, string searchPattern)
        {
            return Directory.GetFileSystemEntries(path, searchPattern);
        }

        public string[] DirectoryGetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.GetFileSystemEntries(path, searchPattern, searchOption);
        }

        public DateTime DirectoryGetLastAccessTime(string path)
        {
            return Directory.GetLastAccessTime(path);
        }

        public DateTime DirectoryGetLastWriteTime(string path)
        {
            return Directory.GetLastWriteTime(path);
        }

        public void DirectoryMove(string sourceDirName, string destDirName)
        {
            Directory.Move(sourceDirName, destDirName);
        }

        public void DirectorySetAccessControl(string path, DirectorySecurity directorySecurity)
        {
            Directory.SetAccessControl(path, directorySecurity);
        }

        public void DirectorySetCreationTime(string path, DateTime creationTime)
        {
            Directory.SetCreationTime(path, creationTime);
        }

        public void DirectorySetCurrentDirectory(string path)
        {
            Directory.SetCurrentDirectory(path);
        }

        public void DirectorySetLastAccessTime(string path, DateTime lastAccessTime)
        {
            Directory.SetLastAccessTime(path, lastAccessTime);
        }

        public void DirectorySetLastWriteTime(string path, DateTime lastWriteTime)
        {
            Directory.SetLastWriteTime(path, lastWriteTime);
        }

        // UTC copies of methods above

        public DateTime DirectoryGetCreationTimeUtc(string path)
        {
            return Directory.GetCreationTimeUtc(path);
        }

        public DateTime DirectoryGetLastAccessTimeUtc(string path)
        {
            return Directory.GetLastAccessTimeUtc(path);
        }

        public DateTime DirectoryGetLastWriteTimeUtc(string path)
        {
            return Directory.GetLastWriteTimeUtc(path);
        }

        public void DirectorySetCreationTimeUtc(string path, DateTime creationTimeUtc)
        {
            Directory.SetCreationTimeUtc(path, creationTimeUtc);
        }

        public void DirectorySetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
        {
            Directory.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
        }

        public void DirectorySetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
        {
            Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        #endregion

        #region Registry access

        public object RegistryGetValue(string keyName, string valueName, object defaultValue)
        {
            return Registry.GetValue(keyName, valueName, defaultValue);
        }

        public void RegistrySetValue(string keyName, string valueName, object value)
        {
            Registry.SetValue(keyName, valueName, value);
        }

        public void RegistrySetValue(string keyName, string valueName, object value, RegistryValueKind valueKind)
        {
            Registry.SetValue(keyName, valueName, value, valueKind);
        }

        #endregion

        #region Miscellaneous

        public IFileChangeWatcherService GetOrCreateFileChangeWatcherService()
        {
            if (this.fileChangeWatcherService == null)
            {
                this.fileChangeWatcherService = new FileChangeWatcherService();
            }
            return this.fileChangeWatcherService;
        }

        /// <summary>
        /// Returns the full (absolute) path name, resolving against the current working directory if needed. Note that this
        /// will handle and return paths that are longer than MAX_PATH. See remarks for full details on differences with
        /// Path.GetFullPath from System.IO.
        /// </summary>
        /// <returns>The full path or null if failed.</returns>
        /// <remarks>
        /// Path.GetFullPath will throw on invalid paths. GetFullPathName only returns null for the most rudimentary failures
        /// (such as passing in an empty string).
        /// 
        /// Path.GetFullPath will attempt to expand short directory and filename segments for the path. GetFullPathName does
        /// not do this.
        /// 
        /// Path.GetFullPath throws for paths that are over MAX_PATH. GetFullPathName does not.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public string MiscGetFullPathName(string path)
        {
            if (path == null) { throw new ArgumentNullException("path"); }

            uint bufferLength = 256; // Can be longer than this, but this will handle most
            StringBuilder finalName = new StringBuilder((int)bufferLength);
            uint returnValue = Interop.UnsafeNativeMethods.GetFullPathName(path, bufferLength, finalName, IntPtr.Zero);
            while (returnValue > bufferLength)
            {
                // Need more room for the output string
                bufferLength = returnValue;
                finalName.EnsureCapacity((int)bufferLength);
                returnValue = Interop.UnsafeNativeMethods.GetFullPathName(path, bufferLength, finalName, IntPtr.Zero);
            }

            if (returnValue == 0)
            {
                // Failed
                int lastError = Marshal.GetLastWin32Error();
                switch (lastError)
                {
                    case 2:     // ERROR_FILE_NOT_FOUND
                    case 3:     // ERROR_PATH_NOT_FOUND
                    case 123:   // ERROR_INVALID_NAME
                    default:
                        Debug.WriteLine(String.Format(CultureInfo.InvariantCulture,
                            "'{0}' returned looking up full path name for '{1}'", lastError, path));
                        break;
                }
                return null;
            }

            return finalName.ToString();
        }

        public bool MiscCanLoadAssembly(string path)
        {      
            throw new NotImplementedException();
        }

        public string MiscGetTempFileName()
        {
            return Path.GetTempFileName();
        }

        public string MiscGetUserSharedTempPath()
        {
            return Path.GetTempPath();
        }

        public bool MiscGetFileAttributes(string path, out FileAttributes attributes)
        {
            return Interop.NativeMethods.GetFileAttributes(path, out attributes);
        }

        public bool MiscPathExists(string path)
        {
            return Interop.NativeMethods.PathExists(path);
        }

        public bool MiscDirectoryExists(string path)
        {
            return Interop.NativeMethods.DirectoryExists(path);
        }

        public bool MiscFileExists(string path)
        {
            return Interop.NativeMethods.FileExists(path);
        }

        public bool MiscSetFileAttributes(string path, FileAttributes attributes)
        {
            return Interop.NativeMethods.SetFileAttributes(path, attributes);
        }

        public void MiscConvertExeToDll(string exeAsDllPath)
        {         
            throw new NotImplementedException();       
        }

        public void LaunchNotepad(string arguments)
        {
            Process.Start("Notepad.exe", arguments);
        }

        public void LaunchWebPage(Uri uri)
        {       
            throw new NotImplementedException();
        }

        public bool SetCursorPos(Point position)
        {
            return Interop.UnsafeNativeMethods.SetCursorPos((int)position.X, (int)position.Y);
        }

        public System.Reflection.AssemblyName GetAssemblyNameFromPath(string path)
        {         
            throw new NotImplementedException();
        }

        public Version GetReferencedVersion(string assemblyPath, string referencedAssemblyName)
        {   
            throw new NotImplementedException();
        }

        public XmlNodeName GetXmlNode(string path, string nodeName, string namespaceUri)
        {
            XmlNodeName xmlNodeName = new XmlNodeName();

            using (FileStream stream = File.OpenRead(path))
            {
                using (XmlReader xmlReader = XmlUtility.CreateXmlReader(stream))
                {
                    while (xmlReader.Read())
                    {
                        if (xmlReader.MoveToContent() == XmlNodeType.Element)
                        {
                            // An example of what we normally look for
                            // xmlReader.GetAttribute("Class", "http://schemas.microsoft.com/winfx/2006/xaml");

                            xmlNodeName.Attribute = xmlReader.GetAttribute(nodeName, namespaceUri);

                            string xmlNamespace = xmlReader.LookupNamespace(xmlReader.Prefix);
                            if (!string.IsNullOrEmpty(xmlNamespace))
                            {
                                xmlNodeName.LocalName = xmlReader.LocalName;
                                xmlNodeName.Namespace = xmlNamespace;

                                return xmlNodeName;
                            }
                        }
                    }
                }
            }

            return xmlNodeName;
        }
        #endregion

        #region FileDataCache helpers
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void CacheSafeDeleteFile(string cacheFile)
        {
            try
            {
                this.FileSetAttributes(cacheFile, FileAttributes.Normal);
                this.FileDelete(cacheFile);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
            }
        }

        public void CacheDeleteMatchingFiles(string path, string fileSpec)
        {
            IEnumerable<string> oldFiles = PathHelper.EnumerateFiles(path, fileSpec, SearchOption.TopDirectoryOnly, throwOnError: false);
            this.DeleteFiles(oldFiles);
        }

        public void CacheDeleteOldMatchingFiles(string path, string fileSpec, DateTime staleUtcTime)
        {
            if (PathHelper.DirectoryExists(path))
            {
                IEnumerable<string> oldFiles = PathHelper.EnumerateFiles(path, fileSpec, SearchOption.TopDirectoryOnly, throwOnError: false)
                        .Where(f => File.GetLastWriteTimeUtc(f) < staleUtcTime);
                this.DeleteFiles(oldFiles);
            }
        }

        /// <summary>
        /// Determines if a given cache file exists and is valid (based on the timestamp).
        /// </summary>
        /// <param name="sourceFileTimestamp">LastWriteTime of the target of the cache file</param>
        /// <param name="cacheFile">Full path name of the cache file of interest</param>
        /// <param name="timestampTolerance">Time difference tolerance, in seconds, for considering timestamps to be equal.</param>
        /// <returns>True if the cache file is valid and will be used when CacheReadCacheFile is called</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public bool CacheIsValid(DateTime sourceFileTimestamp, string cacheFile, double timestampTolerance)
        {
            try
            {
                // Verify that the cache file isn't out-of-date (compare FileGetCreationTimeUtc to assembly's timestamp)
                FileInfo cacheFileInfo = new FileInfo(cacheFile);
                DateTime cacheTimestamp = cacheFileInfo.CreationTimeUtc;

                return Math.Abs((cacheTimestamp - sourceFileTimestamp).TotalSeconds) < timestampTolerance;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public byte[] CacheReadCacheFile(DateTime sourceFileTimestamp, string cacheFile, double timestampTolerance)
        {
            try
            {
                FileInfo cacheFileInfo = new FileInfo(cacheFile);

                // Verify that the cache file isn't out-of-date (compare FileGetCreationTimeUtc to assembly's timestamp)
                DateTime cacheTimestamp = cacheFileInfo.CreationTimeUtc;
                bool outOfTolerance = Math.Abs((cacheTimestamp - sourceFileTimestamp).TotalSeconds) > timestampTolerance;
                if (outOfTolerance)
                {
                    // Cache file invalid.  Delete it and don't use it.
                    return null;
                }

                byte[] fileData = (cacheFileInfo.Length == 0)
                                ? EmptyFileBytes                // Return empty array if file is empty (null would indicate an error)
                                : File.ReadAllBytes(cacheFile);

                // Touch file's LastWriteTime to keep it alive in the cache
                cacheFileInfo.LastWriteTimeUtc = DateTime.UtcNow;

                return fileData;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public bool CacheWriteCacheFile(DateTime sourceFileTimestamp, string cacheFile, byte[] fileData)
        {
            string cachePath = Path.GetDirectoryName(cacheFile);

            // In case user randomly deletes our CachePath, need to make sure it exists
            if (!Directory.Exists(cachePath))
            {
                try
                {
                    Directory.CreateDirectory(cachePath);
                    AccessHelper.AclDirectoryForApplicationPackages(cachePath, rights: FileSystemRights.Read | FileSystemRights.Modify);
                }
                catch (UnauthorizedAccessException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // If we failed to create the directory, we can't proceed
                    if (!Directory.Exists(cachePath))
                    {
                        return false;
                    }
                }
            }

            try
            {
                File.WriteAllBytes(cacheFile, fileData ?? EmptyFileBytes);

                // Set file's CreationTime to match source file's timesamp, and LastWriteTime to indicate last time we used it.
                File.SetCreationTimeUtc(cacheFile, sourceFileTimestamp);
                File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                this.CacheSafeDeleteFile(cacheFile);

                return false;
            }
        }
        #endregion

        public Version EnvironmentOSVersion()
        {
            return Environment.OSVersion.Version;
        }

        private void AddFileLowBoxPermissionsIfNecessary(string path, FileMode mode, bool forceLowBoxPermissions = false)
        {
            if (!this.isWin8)
            {
                return;
            }

            if (forceLowBoxPermissions ||
                path.Contains("AppData") && (mode == FileMode.OpenOrCreate || mode == FileMode.Create || mode == FileMode.CreateNew))
            {
                FileSecurity fSecurity = File.GetAccessControl(path);
                fSecurity.AddAccessRule(new FileSystemAccessRule(AccessHelper.AllApplicationPackagesSecurityIdentifier, FileSystemRights.FullControl, AccessControlType.Allow));
                File.SetAccessControl(path, fSecurity);
            }
        }

        private void AddDirectoryLowBoxPermissionsIfNecessary(string path)
        {
            if (this.isWin8 && path.Contains("AppData"))
            {
                DirectorySecurity dSecurity = Directory.GetAccessControl(path);
                // Note that child objects will inherit these access rights.
                dSecurity.AddAccessRule(
                    new FileSystemAccessRule(
                        AccessHelper.AllApplicationPackagesSecurityIdentifier,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                        PropagationFlags.InheritOnly,
                        AccessControlType.Allow));
                Directory.SetAccessControl(path, dSecurity);
            }
        }

        private void DeleteFiles(IEnumerable<string> filenames)
        {
            foreach (string filename in filenames)
            {
                try
                {
                    this.CacheSafeDeleteFile(filename);
                }
                catch (UnauthorizedAccessException)
                {
                    // when enumerating multiple files, allow the host process to try every file
                    if (OSHelper.IsRunningInAppContainer)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
