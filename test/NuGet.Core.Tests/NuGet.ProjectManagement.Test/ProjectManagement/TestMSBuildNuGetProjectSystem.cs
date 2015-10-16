// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string TestProjectName = "TestProjectName";
        public Dictionary<string, string> References { get; }
        public HashSet<string> FrameworkReferences { get; }
        public HashSet<string> Files { get; }
        private HashSet<string> FilesInProcessing { get; set; }
        public HashSet<string> ProcessedFiles { get; private set; }
        public HashSet<string> Imports { get; }
        public Dictionary<string, int> ScriptsExecuted { get; }
        public int BindingRedirectsCallCount { get; private set; }
        public INuGetProjectContext NuGetProjectContext { get; private set; }

        public TestMSBuildNuGetProjectSystem(NuGetFramework targetFramework, INuGetProjectContext nuGetProjectContext,
            string projectFullPath = null, string projectName = null)
        {
            TargetFramework = targetFramework;
            References = new Dictionary<string, string>();
            FrameworkReferences = new HashSet<string>();
            Files = new HashSet<string>();
            Imports = new HashSet<string>();
            NuGetProjectContext = nuGetProjectContext;
            ProjectFullPath = string.IsNullOrEmpty(projectFullPath) ? Environment.CurrentDirectory : projectFullPath;
            ScriptsExecuted = new Dictionary<string, int>();
            ProcessedFiles = new HashSet<string>();
            ProjectName = projectName ?? TestProjectName;
        }

        public void AddFile(string path, Stream stream)
        {
            if (string.IsNullOrEmpty(path)
                || string.IsNullOrEmpty(Path.GetFileName(path)))
            {
                return;
            }

            using (var streamReader = new StreamReader(stream))
            {
                Files.Add(path);
                var fullPath = Path.Combine(ProjectFullPath, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, streamReader.ReadToEnd());
            }
        }

        public void AddFrameworkReference(string name)
        {
            if (FrameworkReferences.Contains(name))
            {
                throw new InvalidOperationException("Cannot add existing reference. That would be a COMException in VS");
            }
            FrameworkReferences.Add(name);
        }

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            Imports.Add(targetFullPath);
        }

        public void AddReference(string referencePath)
        {
            var referenceAssemblyName = Path.GetFileName(referencePath);
            if (References.ContainsKey(referenceAssemblyName))
            {
                throw new InvalidOperationException("Cannot add existing reference. That would be a COMException in VS");
            }
            References.Add(referenceAssemblyName, referencePath);
        }

        public void RemoveFile(string path)
        {
            Files.Remove(path);
            var fullPath = Path.Combine(ProjectFullPath, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public string ProjectFullPath { get; }

        public string ProjectName { get; }

        public string ProjectUniqueName
        {
            get { return ProjectName; }
        }

        public bool ReferenceExists(string name)
        {
            return References.ContainsKey(name) || FrameworkReferences.Contains(name);
        }

        public void RemoveImport(string targetFullPath)
        {
            Imports.Remove(targetFullPath);
        }

        public void RemoveReference(string name)
        {
            if (References.ContainsKey(name))
            {
                References.Remove(name);
            }
        }

        public NuGetFramework TargetFramework { get; }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            NuGetProjectContext = nuGetProjectContext;
        }

        public bool FileExistsInProject(string path)
        {
            return Files.Where(c => path.Equals(c, StringComparison.OrdinalIgnoreCase)).Any();
        }

        public dynamic GetPropertyValue(string propertyName)
        {
            throw new NotImplementedException();
        }

        public string ResolvePath(string path)
        {
            return path;
        }

        public bool IsSupportedFile(string path)
        {
            return true;
        }

        public void AddExistingFile(string path)
        {
            Files.Add(path);
        }

        public void AddBindingRedirects()
        {
            BindingRedirectsCallCount++;
        }

        public Task ExecuteScriptAsync(PackageIdentity identity, string packageInstallPath, string scriptRelativePath, NuGetProject nuGetProject, bool throwOnFailure)
        {
            var scriptFullPath = Path.Combine(packageInstallPath, scriptRelativePath);
            if (!File.Exists(scriptFullPath) && throwOnFailure)
            {
                throw new InvalidOperationException(scriptRelativePath + " was not found. Could not execute PS script");
            }

            int runCount;
            if (!ScriptsExecuted.TryGetValue(scriptRelativePath, out runCount))
            {
                ScriptsExecuted.Add(scriptRelativePath, 0);
            }

            ScriptsExecuted[scriptRelativePath]++;
            return Task.FromResult(0);
        }

        public void BeginProcessing()
        {
        }

        public void RegisterProcessedFiles(IEnumerable<string> files)
        {
            if (FilesInProcessing == null)
            {
                FilesInProcessing = new HashSet<string>(files);
            }

            foreach (var file in files)
            {
                FilesInProcessing.Add(file);
            }
        }

        public void EndProcessing()
        {
            ProcessedFiles = FilesInProcessing;
            FilesInProcessing = null;
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            // no-op
            var fullPath = Path.Combine(ProjectFullPath, path);
            Directory.Delete(fullPath, recursive: false);
        }

        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            return Files.Where(f => f.StartsWith(path));
        }

        public IEnumerable<string> GetFullPaths(string fileName)
        {
            return Files.Where(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.Combine(ProjectFullPath, f));
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return GetFiles(path, "*.*", recursive: true);
        }
    }
}
