// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Note: This was taken from:
// https://github.com/Microsoft/msbuild/blob/6b395a35e3ef2353a5473a718c2262d1f095fd2c/src/Shared/UnitTests/TestEnvironment.cs
// It is only used for test purposes and no need to keep in sync. All of the test dependencies are included in this file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;
using Xunit.Abstractions;

using TempPaths = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    public class TestEnvironment : IDisposable
    {
        /// <summary>
        ///     List of test invariants to assert value does not change.
        /// </summary>
        private readonly List<TestInvariant> _invariants = new List<TestInvariant>();

        /// <summary>
        ///     List of test variants which need to be reverted when the test completes.
        /// </summary>
        private readonly List<TransientTestState> _variants = new List<TransientTestState>();

        private readonly ITestOutputHelper _output;

        private readonly Lazy<TransientTestFolder> _defaultTestDirectory;

        private bool _disposed;

        public TransientTestFolder DefaultTestDirectory => _defaultTestDirectory.Value;

        public static TestEnvironment Create(ITestOutputHelper output = null, bool ignoreBuildErrorFiles = false, bool setDefaultInvariants = false)
        {
            var env = new TestEnvironment(output ?? new DefaultOutput(), setDefaultInvariants);

            // In most cases, if MSBuild wrote an MSBuild_*.txt to the temp path something went wrong.
            if (!ignoreBuildErrorFiles)
            {
                env.WithInvariant(new BuildFailureLogInvariant());
            }

            env.SetEnvironmentVariable("MSBUILDRELOADTRAITSONEACHACCESS", "1");

            return env;
        }

        private TestEnvironment(ITestOutputHelper output, bool setDefaultInvariants = true)
        {
            _output = output;
            _defaultTestDirectory = new Lazy<TransientTestFolder>(() => CreateFolder());
            if (setDefaultInvariants) SetDefaultInvariant();
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }

        ~TestEnvironment()
        {
            Cleanup();
        }

        /// <summary>
        ///     Revert / cleanup variants and then assert invariants.
        /// </summary>
        private void Cleanup()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Reset test variants
                foreach (var variant in _variants)
                    variant.Revert();

                // Assert invariants
                foreach (var item in _invariants)
                    item.AssertInvariant(_output);
            }
        }

        /// <summary>
        ///     Evaluate the test with the given invariant.
        /// </summary>
        /// <param name="invariant">Test invariant to assert unchanged on completion.</param>
        public T WithInvariant<T>(T invariant) where T : TestInvariant
        {
            _invariants.Add(invariant);
            return invariant;
        }

        /// <summary>
        ///     Evaluate the test with the given transient test state.
        /// </summary>
        /// <returns>Test state to revert on completion.</returns>
        public T WithTransientTestState<T>(T transientState) where T : TransientTestState
        {
            _variants.Add(transientState);
            return transientState;
        }

        /// <summary>
        ///     Clears all test invariants. This should only be used if there is a known
        ///     issue with a test!
        /// </summary>
        public void ClearTestInvariants()
        {
            _invariants.Clear();
        }

        #region Common test variants

        private void SetDefaultInvariant()
        {
            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Path.GetTempPath()", Path.GetTempPath));

            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Directory.GetCurrentDirectory", Directory.GetCurrentDirectory));

            WithEnvironmentInvariant();
        }

        /// <summary>
        ///     Creates a test invariant that asserts an environment variable does not change during the test.
        /// </summary>
        /// <param name="environmentVariableName">Name of the environment variable.</param>
        public TestInvariant WithEnvironmentVariableInvariant(string environmentVariableName)
        {
            return WithInvariant(new StringInvariant(environmentVariableName,
                () => Environment.GetEnvironmentVariable(environmentVariableName)));
        }
        /// <summary>
        /// Creates a test invariant which asserts that the environment variables do not change
        /// </summary>
        public TestInvariant WithEnvironmentInvariant()
        {
            return WithInvariant(new EnvironmentInvariant());
        }

        /// <summary>
        ///     Creates a string invariant that will assert the value is the same before and after the test.
        /// </summary>
        /// <param name="name">Name of the item to keep track of.</param>
        /// <param name="value">Delegate to get the value for the invariant.</param>
        public TestInvariant WithStringInvariant(string name, Func<string> value)
        {
            return WithInvariant(new StringInvariant(name, value));
        }

        /// <summary>
        /// Creates a new temp path
        /// </summary>
        public TransientTempPath CreateNewTempPath()
        {
            var folder = CreateFolder();
            return SetTempPath(folder.FolderPath, true);
        }

        /// <summary>
        /// Creates a new temp path
        /// Sets all OS temp environment variables to the new path
        ///
        /// Cleanup:
        /// - restores OS temp environment variables
        /// </summary>
        public TransientTempPath SetTempPath(string tempPath, bool deleteTempDirectory = false)
        {
            var transientTempPath = new TransientTempPath(tempPath, deleteTempDirectory);
            _variants.Add(transientTempPath);

            return transientTempPath;
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file which will be deleted when the test completes.
        /// </summary>
        /// <param name="extension">Extensions of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile:true, expectedAsOutput:false));
        }

        public TransientTestFile CreateFile(string fileName, string contents = "")
        {
            return CreateFile(DefaultTestDirectory, fileName, contents);
        }

        public TransientTestFile CreateFile(TransientTestFolder transientTestFolder, string fileName, string contents = "")
        {
            var file = WithTransientTestState(new TransientTestFile(transientTestFolder.FolderPath, Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName)));
            File.WriteAllText(file.Path, contents);

            return file;
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file under a specific temporary folder. File will
        ///     be cleaned up when the test completes.
        /// </summary>
        /// <param name="transientTestFolder"></param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.FolderPath, extension,
                createFile: true, expectedAsOutput: false));
        }


        /// <summary>
        ///     Gets a transient test file associated with a unique file name but does not create the file.
        /// </summary>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile GetTempFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile: false, expectedAsOutput: false));
        }

        /// <summary>
        ///     Gets a transient test file under a specified folder associated with a unique file name but does not create the file.
        /// </summary>
        /// <param name="transientTestFolder">Temp folder</param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile GetTempFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.FolderPath, extension,
                createFile: false, expectedAsOutput: false));
        }

        /// <summary>
        ///     Create a temp file name that is expected to exist when the test completes.
        /// </summary>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile ExpectFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile: false, expectedAsOutput: true));
        }

        /// <summary>
        /// Create a temp file name under a specific temporary folder. The file is expected to exist when the test completes.
        /// </summary>
        /// <param name="transientTestFolder">Temp folder</param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile ExpectFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.FolderPath, extension, createFile: false, expectedAsOutput: true));
        }

        /// <summary>
        ///     Creates a test variant used to add a unique temporary folder during a test. Will be deleted when the test
        ///     completes.
        /// </summary>
        public TransientTestFolder CreateFolder(string folderPath = null, bool createFolder = true)
        {
            var folder = WithTransientTestState(new TransientTestFolder(folderPath, createFolder));

            Assert.True(!(createFolder ^ Directory.Exists(folder.FolderPath)));

            return folder;
        }

        /// <summary>
        ///     Creates a test variant used to add a unique temporary folder during a test. Will be deleted when the test
        ///     completes.
        /// </summary>
        public TransientTestFolder CreateFolder(bool createFolder)
        {
            return CreateFolder(null, createFolder);
        }

        /// <summary>
        ///     Create an test variant used to change the value of an environment variable during a test. Original value
        ///     will be restored when complete.
        /// </summary>
        public TransientTestState SetEnvironmentVariable(string environmentVariableName, string newValue)
        {
            return WithTransientTestState(new TransientTestEnvironmentVariable(environmentVariableName, newValue));
        }

        public TransientTestState SetCurrentDirectory(string newWorkingDirectory)
        {
            return WithTransientTestState(new TransientWorkingDirectory(newWorkingDirectory));
        }

        #endregion

        private class DefaultOutput : ITestOutputHelper
        {
            public void WriteLine(string message)
            {
                Console.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                Console.WriteLine(format, args);
            }
        }

        

        /// <summary>
        ///     Creates a test variant representing a test project with files relative to the project root. All files
        ///     and the root will be cleaned up when the test completes.
        /// </summary>
        /// <param name="projectContents">Contents of the project file to be created.</param>
        /// <param name="files">Files to be created.</param>
        /// <param name="relativePathFromRootToProject">Path for the specified files to be created in relative to 
        /// the root of the project directory.</param>
        public TransientTestProjectWithFiles CreateTestProjectWithFiles(string projectContents, string[] files = null, string relativePathFromRootToProject = ".")
        {
            return WithTransientTestState(
                new TransientTestProjectWithFiles(projectContents, files, relativePathFromRootToProject));
        }
    }

    /// <summary>
    ///     Things that are expected not to change and should be asserted before and after running.
    /// </summary>
    public abstract class TestInvariant
    {
        public abstract void AssertInvariant(ITestOutputHelper output);
    }

    /// <summary>
    ///     Things that are expected to change and should be reverted after running.
    /// </summary>
    public abstract class TransientTestState
    {
        public abstract void Revert();
    }

    public class StringInvariant : TestInvariant
    {
        private readonly Func<string> _accessorFunc;
        private readonly string _name;
        private readonly string _originalValue;

        public StringInvariant(string name, Func<string> accessorFunc)
        {
            _name = name;
            _accessorFunc = accessorFunc;
            _originalValue = accessorFunc();
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var currentValue = _accessorFunc();

            //  Something like the following might be preferrable, but the assertion method truncates the values leaving us without
            //  useful information.  So use Assert.True instead
            //  Assert.Equal($"{_name}: {_originalValue}", $"{_name}: {_accessorFunc()}");

            Assert.True(currentValue == _originalValue, $"Expected {_name} to be '{_originalValue}', but it was '{currentValue}'");
        }
    }

    public class EnvironmentInvariant : TestInvariant
    {
        private readonly IDictionary _initialEnvironment;

        public EnvironmentInvariant()
        {
            _initialEnvironment = Environment.GetEnvironmentVariables();
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var environment = Environment.GetEnvironmentVariables();

            AssertDictionaryInclusion(_initialEnvironment, environment, "added");
            AssertDictionaryInclusion(environment, _initialEnvironment, "removed");

            // a includes b
            void AssertDictionaryInclusion(IDictionary a, IDictionary b, string operation)
            {
                foreach (var key in b.Keys)
                {
                    a.Contains(key).Should().Be(true, $"environment variable {operation}: {key}");
                    a[key].Should().Be(b[key]);
                }
            }
        }
    }

    public class BuildFailureLogInvariant : TestInvariant
    {
        private readonly string[] _originalFiles;

        public BuildFailureLogInvariant()
        {
            _originalFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var newFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");

            var newFilesCount = newFiles.Length;
            if (newFilesCount > _originalFiles.Length)
            {
                foreach (var file in newFiles.Except(_originalFiles).Select(f => new FileInfo(f)))
                {
                    var contents = File.ReadAllText(file.FullName);

                    // Delete the file so we don't pollute the build machine
                    FileUtilities.DeleteNoThrow(file.FullName);

                    // Ignore clean shutdown trace logs.
                    if (Regex.IsMatch(file.Name, @"MSBuild_NodeShutdown_\d+\.txt") &&
                        Regex.IsMatch(contents, @"Node shutting down with reason BuildComplete and exception:\s*"))
                    {
                        newFilesCount--;
                        continue;
                    }

                    // Com trace file. This is probably fine, but output it as it was likely turned on
                    // for a reason.
                    if (Regex.IsMatch(file.Name, @"MSBuild_CommTrace_PID_\d+\.txt"))
                    {
                        output.WriteLine($"{file.Name}: {contents}");
                        newFilesCount--;
                        continue;
                    }

                    output.WriteLine($"Build Error File {file.Name}: {contents}");
                }
            }

            // Assert file count is equal minus any files that were OK
            Assert.Equal(_originalFiles.Length, newFilesCount);
        }
    }

    public class TransientTempPath : TransientTestState
    {
        private const string TMP = "TMP";
        private const string TMPDIR = "TMPDIR";
        private const string TEMP = "TEMP";

        private readonly bool _deleteTempDirectory;

        private readonly TempPaths _oldtempPaths;

        public string TempPath { get; }

        public TransientTempPath(string tempPath, bool deleteTempDirectory)
        {
            TempPath = tempPath;
            _deleteTempDirectory = deleteTempDirectory;

            _oldtempPaths = SetTempPath(tempPath);
        }

        private static TempPaths SetTempPath(string tempPath)
        {
            var oldTempPaths = GetTempPaths();

            foreach (var key in oldTempPaths.Keys)
            {
                Environment.SetEnvironmentVariable(key, tempPath);
            }

            return oldTempPaths;
        }

        private static TempPaths SetTempPaths(TempPaths tempPaths)
        {
            var oldTempPaths = GetTempPaths();

            foreach (var key in oldTempPaths.Keys)
            {
                Environment.SetEnvironmentVariable(key, tempPaths[key]);
            }

            return oldTempPaths;
        }

        private static TempPaths GetTempPaths()
        {
            var tempPaths = new TempPaths
            {
                [TMP] = Environment.GetEnvironmentVariable(TMP),
                [TEMP] = Environment.GetEnvironmentVariable(TEMP)
            };

            if (NuGet.Common.RuntimeEnvironmentHelper.IsLinux || NuGet.Common.RuntimeEnvironmentHelper.IsMacOSX)
            {
                tempPaths[TMPDIR] = Environment.GetEnvironmentVariable(TMPDIR);
            }

            return tempPaths;
        }

        public override void Revert()
        {
            SetTempPaths(_oldtempPaths);

            if (_deleteTempDirectory)
            {
                FileUtilities.DeleteDirectoryNoThrow(TempPath, recursive: true);
            }
        }
    }


    public class TransientTestFile : TransientTestState
    {
        private readonly bool _createFile;
        private readonly bool _expectedAsOutput;

        public TransientTestFile(string extension, bool createFile, bool expectedAsOutput)
        {
            _createFile = createFile;
            _expectedAsOutput = expectedAsOutput;
            Path = FileUtilities.GetTemporaryFile(null, extension, createFile);
        }

        public TransientTestFile(string rootPath, string extension, bool createFile, bool expectedAsOutput)
        {
            _createFile = createFile;
            _expectedAsOutput = expectedAsOutput;
            Path = FileUtilities.GetTemporaryFile(rootPath, extension, createFile);
        }

        public TransientTestFile(string rootPath, string fileNameWithoutExtension, string extension)
        {
            Path = System.IO.Path.Combine(rootPath, fileNameWithoutExtension + extension);

            File.WriteAllText(Path, string.Empty);
        }

        public string Path { get; }

        public override void Revert()
        {
            try
            {
                if (_expectedAsOutput)
                {
                    Assert.True(File.Exists(Path), $"A file expected as an output does not exist: {Path}");
                }
            }
            finally
            {
                FileUtilities.DeleteNoThrow(Path);
            }
        }
    }

    public class TransientTestFolder : TransientTestState
    {
        public TransientTestFolder(string folderPath = null, bool createFolder = true)
        {
            FolderPath = folderPath ?? FileUtilities.GetTemporaryDirectory(createFolder);

            if (createFolder)
            {
                Directory.CreateDirectory(FolderPath);
            }
        }

        public string FolderPath { get; }

        public override void Revert()
        {
            // Basic checks to make sure we're not deleting something very obviously wrong (e.g.
            // the entire temp drive).
            Assert.NotNull(FolderPath);
            Assert.NotEqual(string.Empty, FolderPath);
            Assert.NotEqual(@"\", FolderPath);
            Assert.NotEqual(@"/", FolderPath);
            Assert.NotEqual(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(FolderPath));
            Assert.True(Path.IsPathRooted(FolderPath));

            FileUtilities.DeleteDirectoryNoThrow(FolderPath, true);
        }
    }

    public class TransientTestEnvironmentVariable : TransientTestState
    {
        private readonly string _environmentVariableName;
        private readonly string _originalValue;

        public TransientTestEnvironmentVariable(string environmentVariableName, string newValue)
        {
            _environmentVariableName = environmentVariableName;
            _originalValue = Environment.GetEnvironmentVariable(environmentVariableName);

            Environment.SetEnvironmentVariable(environmentVariableName, newValue);
        }

        public override void Revert()
        {
            Environment.SetEnvironmentVariable(_environmentVariableName, _originalValue);
        }
    }

    public class TransientWorkingDirectory : TransientTestState
    {
        private readonly string _originalValue;

        public TransientWorkingDirectory(string newWorkingDirectory)
        {
            _originalValue = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newWorkingDirectory);
        }

        public override void Revert()
        {
            Directory.SetCurrentDirectory(_originalValue);
        }
    }

    public class TransientTestProjectWithFiles : TransientTestState
    {
        private readonly TransientTestFolder _folder;

        public string TestRoot => _folder.FolderPath;

        public string[] CreatedFiles { get; }

        public string ProjectFile { get; }

        public TransientTestProjectWithFiles(string projectContents, string[] files,
            string relativePathFromRootToProject = ".")
        {
            _folder = new TransientTestFolder();

            var projectDir = Path.Combine(TestRoot, relativePathFromRootToProject);
            Directory.CreateDirectory(projectDir);

            ProjectFile = Path.Combine(projectDir, "build.proj");
            File.WriteAllText(ProjectFile, FileUtilities.CleanupFileContents(projectContents));

            CreatedFiles = FileUtilities.CreateFilesInDirectory(TestRoot, files);
        }

        public override void Revert()
        {
            _folder.Revert();
        }
    }

    public static class FileUtilities
    {
        internal static void DeleteDirectoryNoThrow(string path, bool recursive, int retryCount = 0, int retryTimeOut = 0)
        {
            // Try parse will set the out parameter to 0 if the string passed in is null, or is outside the range of an int.
            if (!int.TryParse(Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETERETRYCOUNT"), out retryCount))
            {
                retryCount = 0;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETRETRYTIMEOUT"), out retryTimeOut))
            {
                retryTimeOut = 0;
            }

            retryCount = retryCount < 1 ? 2 : retryCount;
            retryTimeOut = retryTimeOut < 1 ? 500 : retryTimeOut;

            path = FixFilePath(path);

            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive);
                        break;
                    }
                }
                catch (Exception ex) when (IsIoRelatedException(ex))
                {
                }

                if (i + 1 < retryCount) // should not wait for the final iteration since we not gonna check anyway
                {
                    Thread.Sleep(retryTimeOut);
                }
            }
        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');//.Replace("//", "/");
        }

        /// <summary>
        /// Generates a unique directory name in the temporary folder.  
        /// Caller must delete when finished. 
        /// </summary>
        /// <param name="createDirectory"></param>
        internal static string GetTemporaryDirectory(bool createDirectory = true)
        {
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Temporary" + Guid.NewGuid().ToString("N"));

            if (createDirectory)
            {
                Directory.CreateDirectory(temporaryDirectory);
            }

            return temporaryDirectory;
        }

        /// <summary>
        /// A variation on File.Delete that will throw ExceptionHandling.NotExpectedException exceptions
        /// </summary>
        internal static void DeleteNoThrow(string path)
        {
            try
            {
                File.Delete(FixFilePath(path));
            }
            catch (Exception ex) when (IsIoRelatedException(ex))
            {
            }
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// If no extension is provided, uses ".tmp".
        /// File is guaranteed to be unique.
        /// Caller must delete it when finished.
        /// </summary>
        internal static string GetTemporaryFile()
        {
            return GetTemporaryFile(".tmp");
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string extension)
        {
            return GetTemporaryFile(null, extension);
        }

        /// <summary>
        /// Creates a file with unique temporary file name with a given extension in the specified folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// If folder is null, the temporary folder will be used.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string directory, string extension, bool createFile = true)
        {
            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            directory = directory ?? Path.GetTempPath();

            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, $"tmp{Guid.NewGuid():N}{extension}");

            if (createFile)
            {
                File.WriteAllText(file, string.Empty);
            }

            return file;
        }

        internal static bool IsIoRelatedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }

        /// <summary>
        /// Does certain replacements in a string representing the project file contents.
        /// This makes it easier to write unit tests because the author doesn't have
        /// to worry about escaping double-quotes, etc.
        /// </summary>
        /// <param name="projectFileContents"></param>
        /// <returns></returns>
        internal static string CleanupFileContents(string projectFileContents)
        {
            // Replace reverse-single-quotes with double-quotes.
            projectFileContents = projectFileContents.Replace("`", "\"");

            // Place the correct MSBuild namespace into the <Project> tag.
            projectFileContents = projectFileContents.Replace("msbuildnamespace", "http://schemas.microsoft.com/developer/msbuild/2003");
            projectFileContents = projectFileContents.Replace("msbuilddefaulttoolsversion", "15.0");
            projectFileContents = projectFileContents.Replace("msbuildassemblyversion", "15.1");

            return projectFileContents;
        }

        /// <summary>
        /// Creates a bunch of temporary files in the given directory with the specified names and returns
        /// their full paths (so they can ultimately be cleaned up)
        /// </summary>
        internal static string[] CreateFilesInDirectory(string rootDirectory, params string[] files)
        {
            if (files == null)
            {
                return null;
            }

            Assert.True(Directory.Exists(rootDirectory), $"Directory {rootDirectory} does not exist");

            var result = new string[files.Length];

            for (var i = 0; i < files.Length; i++)
            {
                // On Unix there is the risk of creating one file with '\' in its name instead of directories.
                // Therefore split the arguments into path fragments and recompose the path.
                var fileFragments = SplitPathIntoFragments(files[i]);
                var rootDirectoryFragments = SplitPathIntoFragments(rootDirectory);
                var pathFragments = rootDirectoryFragments.Concat(fileFragments);

                var fullPath = Path.Combine(pathFragments.ToArray());

                var directoryName = Path.GetDirectoryName(fullPath);

                Directory.CreateDirectory(directoryName);
                Assert.True(Directory.Exists(directoryName));

                File.WriteAllText(fullPath, string.Empty);
                Assert.True(File.Exists(fullPath));

                result[i] = fullPath;
            }

            return result;
        }

        private static string[] SplitPathIntoFragments(string path)
        {
            // Both Path.AltDirectorSeparatorChar and Path.DirectorySeparator char return '/' on OSX,
            // which renders them useless for the following case where I want to split a path that may contain either separator
            var splits = path.Split('/', '\\');

            // if the path is rooted then the first split is either empty (Unix) or 'c:' (Windows)
            // in this case the root must be restored back to '/' (Unix) or 'c:\' (Windows)
            if (Path.IsPathRooted(path))
            {
                splits[0] = Path.GetPathRoot(path);
            }

            return splits;
        }

    }

    #region MockLogger
    /*
     * Class:   MockLogger
     *
     * Mock logger class. Keeps track of errors and warnings and also builds
     * up a raw string (fullLog) that contains all messages, warnings, errors.
     *
     */
    internal sealed class MockLogger : ILogger
    {
        #region Properties

        private StringBuilder _fullLog = new StringBuilder();
        private readonly ITestOutputHelper _testOutputHelper;

        /// <summary>
        /// Should the build finished event be logged in the log file. This is to work around the fact we have different
        /// localized strings between env and xmake for the build finished event.
        /// </summary>
        internal bool LogBuildFinished { get; set; } = true;

        /*
         * Method:  ErrorCount
         *
         * The count of all errors seen so far.
         *
         */
        internal int ErrorCount { get; private set; } = 0;

        /*
         * Method:  WarningCount
         *
         * The count of all warnings seen so far.
         *
         */
        internal int WarningCount { get; private set; } = 0;

        /// <summary>
        /// Return the list of logged errors
        /// </summary>
        internal List<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();

        /// <summary>
        /// Returns the list of logged warnings
        /// </summary>
        internal List<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

        /// <summary>
        /// When set to true, allows task crashes to be logged without causing an assert.
        /// </summary>
        internal bool AllowTaskCrashes
        {
            get;
            set;
        }

        /// <summary>
        /// List of ExternalProjectStarted events
        /// </summary>
        internal List<ExternalProjectStartedEventArgs> ExternalProjectStartedEvents { get; } = new List<ExternalProjectStartedEventArgs>();

        /// <summary>
        /// List of ExternalProjectFinished events
        /// </summary>
        internal List<ExternalProjectFinishedEventArgs> ExternalProjectFinishedEvents { get; } = new List<ExternalProjectFinishedEventArgs>();

        /// <summary>
        /// List of ProjectStarted events
        /// </summary>
        internal List<ProjectStartedEventArgs> ProjectStartedEvents { get; } = new List<ProjectStartedEventArgs>();

        /// <summary>
        /// List of ProjectFinished events
        /// </summary>
        internal List<ProjectFinishedEventArgs> ProjectFinishedEvents { get; } = new List<ProjectFinishedEventArgs>();

        /// <summary>
        /// List of TargetStarted events
        /// </summary>
        internal List<TargetStartedEventArgs> TargetStartedEvents { get; } = new List<TargetStartedEventArgs>();

        /// <summary>
        /// List of TargetFinished events
        /// </summary>
        internal List<TargetFinishedEventArgs> TargetFinishedEvents { get; } = new List<TargetFinishedEventArgs>();

        /// <summary>
        /// List of TaskStarted events
        /// </summary>
        internal List<TaskStartedEventArgs> TaskStartedEvents { get; } = new List<TaskStartedEventArgs>();

        /// <summary>
        /// List of TaskFinished events
        /// </summary>
        internal List<TaskFinishedEventArgs> TaskFinishedEvents { get; } = new List<TaskFinishedEventArgs>();

        /// <summary>
        /// List of BuildMessage events
        /// </summary>
        internal List<BuildMessageEventArgs> BuildMessageEvents { get; } = new List<BuildMessageEventArgs>();

        /// <summary>
        /// List of BuildStarted events, thought we expect there to only be one, a valid check is to make sure this list is length 1
        /// </summary>
        internal List<BuildStartedEventArgs> BuildStartedEvents { get; } = new List<BuildStartedEventArgs>();

        /// <summary>
        /// List of BuildFinished events, thought we expect there to only be one, a valid check is to make sure this list is length 1
        /// </summary>
        internal List<BuildFinishedEventArgs> BuildFinishedEvents { get; } = new List<BuildFinishedEventArgs>();

        internal List<BuildEventArgs> AllBuildEvents { get; } = new List<BuildEventArgs>();

        /*
         * Method:  FullLog
         *
         * The raw concatenation of all messages, errors and warnings seen so far.
         *
         */
        internal string FullLog => _fullLog.ToString();

        #endregion

        #region Minimal ILogger implementation

        /*
         * Property:    Verbosity
         *
         * The level of detail to show in the event log.
         *
         */
        public LoggerVerbosity Verbosity
        {
            get => LoggerVerbosity.Normal;
            set {/* do nothing */}
        }

        /*
         * Property:    Parameters
         * 
         * The mock logger does not take parameters.
         * 
         */
        public string Parameters
        {
            get => null;

            set
            {
                // do nothing
            }
        }

        /*
         * Method:  Initialize
         *
         * Add a new build event.
         *
         */
        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += LoggerEventHandler;
        }

        /// <summary>
        /// Clears the content of the log "file"
        /// </summary>
        public void ClearLog()
        {
            _fullLog = new StringBuilder();
        }

        /*
         * Method:  Shutdown
         * 
         * The mock logger does not need to release any resources.
         * 
         */
        public void Shutdown()
        {
            // do nothing
        }
        #endregion

        public MockLogger()
        {
            _testOutputHelper = null;
        }

        public MockLogger(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public List<Action<object, BuildEventArgs>> AdditionalHandlers { get; set; } = new List<Action<object, BuildEventArgs>>();

        /*
         * Method:  LoggerEventHandler
         *
         * Receives build events and logs them the way we like.
         *
         */
        internal void LoggerEventHandler(object sender, BuildEventArgs eventArgs)
        {
            AllBuildEvents.Add(eventArgs);

            foreach (var handler in AdditionalHandlers)
            {
                handler(sender, eventArgs);
            }

            if (eventArgs is BuildWarningEventArgs)
            {
                var w = (BuildWarningEventArgs)eventArgs;

                // hack: disregard the MTA warning.
                // need the second condition to pass on ploc builds
                if (w.Code != "MSB4056" && !w.Message.Contains("MSB4056"))
                {
                    var logMessage =
                        $"{w.File}({w.LineNumber},{w.ColumnNumber}): {w.Subcategory} warning {w.Code}: {w.Message}";

                    _fullLog.AppendLine(logMessage);
                    _testOutputHelper?.WriteLine(logMessage);

                    ++WarningCount;
                    Warnings.Add(w);
                }
            }
            else if (eventArgs is BuildErrorEventArgs)
            {
                var e = (BuildErrorEventArgs)eventArgs;

                var logMessage =
                    $"{e.File}({e.LineNumber},{e.ColumnNumber}): {e.Subcategory} error {e.Code}: {e.Message}";
                _fullLog.AppendLine(logMessage);
                _testOutputHelper?.WriteLine(logMessage);

                ++ErrorCount;
                Errors.Add(e);
            }
            else
            {
                // Log the message unless we are a build finished event and logBuildFinished is set to false.
                var logMessage = !(eventArgs is BuildFinishedEventArgs) ||
                                  (eventArgs is BuildFinishedEventArgs && LogBuildFinished);
                if (logMessage)
                {
                    _fullLog.AppendLine(eventArgs.Message);
                    _testOutputHelper?.WriteLine(eventArgs.Message);
                }
            }

            if (eventArgs is ExternalProjectStartedEventArgs)
            {
                ExternalProjectStartedEvents.Add((ExternalProjectStartedEventArgs)eventArgs);
            }
            else if (eventArgs is ExternalProjectFinishedEventArgs)
            {
                ExternalProjectFinishedEvents.Add((ExternalProjectFinishedEventArgs)eventArgs);
            }

            if (eventArgs is ProjectStartedEventArgs)
            {
                ProjectStartedEvents.Add((ProjectStartedEventArgs)eventArgs);
            }
            else if (eventArgs is ProjectFinishedEventArgs)
            {
                ProjectFinishedEvents.Add((ProjectFinishedEventArgs)eventArgs);
            }
            else if (eventArgs is TargetStartedEventArgs)
            {
                TargetStartedEvents.Add((TargetStartedEventArgs)eventArgs);
            }
            else if (eventArgs is TargetFinishedEventArgs)
            {
                TargetFinishedEvents.Add((TargetFinishedEventArgs)eventArgs);
            }
            else if (eventArgs is TaskStartedEventArgs)
            {
                TaskStartedEvents.Add((TaskStartedEventArgs)eventArgs);
            }
            else if (eventArgs is TaskFinishedEventArgs)
            {
                TaskFinishedEvents.Add((TaskFinishedEventArgs)eventArgs);
            }
            else if (eventArgs is BuildMessageEventArgs)
            {
                BuildMessageEvents.Add((BuildMessageEventArgs)eventArgs);
            }
            else if (eventArgs is BuildStartedEventArgs)
            {
                BuildStartedEvents.Add((BuildStartedEventArgs)eventArgs);
            }
            else if (eventArgs is BuildFinishedEventArgs)
            {
                BuildFinishedEvents.Add((BuildFinishedEventArgs)eventArgs);

                if (!AllowTaskCrashes)
                {
                    // We should not have any task crashes. Sometimes a test will validate that their expected error
                    // code appeared, but not realize it then crashed.
                    AssertLogDoesntContain("MSB4018");
                }

                // We should not have any Engine crashes.
                AssertLogDoesntContain("MSB0001");

                // Console.Write in the context of a unit test is very expensive.  A hundred
                // calls to Console.Write can easily take two seconds on a fast machine.  Therefore, only
                // do the Console.Write once at the end of the build.
                Console.Write(FullLog);
            }
        }

        /// <summary>
        /// Assert that the log file contains the given strings, in order.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(params string[] contains)
        {
            AssertLogContains(true, contains);
        }

        /// <summary>
        /// Assert that the log file contains the given string, in order. Includes the option of case invariance
        /// </summary>
        /// <param name="isCaseSensitive">False if we do not care about case sensitivity</param>
        /// <param name="contains"></param>
        internal void AssertLogContains(bool isCaseSensitive, params string[] contains)
        {
            var reader = new StringReader(FullLog);
            var index = 0;

            var currentLine = reader.ReadLine();
            if (!isCaseSensitive)
            {
                currentLine = currentLine.ToUpper();
            }

            while (currentLine != null)
            {
                var comparer = contains[index];
                if (!isCaseSensitive)
                {
                    comparer = comparer.ToUpper();
                }

                if (currentLine.Contains(comparer))
                {
                    index++;
                    if (index == contains.Length) break;
                }

                currentLine = reader.ReadLine();
                if (!isCaseSensitive)
                {
                    currentLine = currentLine?.ToUpper();
                }
            }
            if (index != contains.Length)
            {
                if (_testOutputHelper != null)
                {
                    _testOutputHelper.WriteLine(FullLog);
                }
                else
                {
                    Console.WriteLine(FullLog);
                }
                Assert.True(false, $"Log was expected to contain '{contains[index]}', but did not.\n=======\n{FullLog}\n=======");
            }
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            if (FullLog.Contains(contains))
            {
                if (_testOutputHelper != null)
                {
                    _testOutputHelper.WriteLine(FullLog);
                }
                else
                {
                    Console.WriteLine(FullLog);
                }
                Assert.True(false, $"Log was not expected to contain '{contains}', but did.");
            }
        }

        /// <summary>
        /// Assert that no errors were logged
        /// </summary>
        internal void AssertNoErrors()
        {
            Assert.Equal(0, ErrorCount);
        }

        /// <summary>
        /// Assert that no warnings were logged
        /// </summary>
        internal void AssertNoWarnings()
        {
            Assert.Equal(0, WarningCount);
        }
    }
#endregion
}
