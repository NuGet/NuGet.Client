// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Test.Utility;

namespace Test.Utility
{
    public class TestSolutionManager : ISolutionManager, IDisposable
    {
        public List<NuGetProject> NuGetProjects { get; set; } = new List<NuGetProject>();
        public INuGetProjectContext NuGetProjectContext { get; set; } = new TestNuGetProjectContext();

        public string SolutionDirectory { get; }

        private const string PackagesFolder = "packages";

        private TestDirectory _testDirectory;

        public TestSolutionManager(bool foo)
        {
            _testDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            SolutionDirectory = _testDirectory;
        }

        public TestSolutionManager(string solutionDirectory)
        {
            if (solutionDirectory == null)
            {
                throw new ArgumentNullException(nameof(solutionDirectory));
            }

            SolutionDirectory = solutionDirectory;
        }

        public MSBuildNuGetProject AddNewMSBuildProject(string projectName = null, NuGetFramework projectTargetFramework = null, string packagesConfigName = null)
        {
            if (GetNuGetProject(projectName) != null)
            {
                throw new ArgumentException("Project with " + projectName + " already exists");
            }

            var packagesFolder = Path.Combine(SolutionDirectory, PackagesFolder);
            projectName = string.IsNullOrEmpty(projectName) ? Guid.NewGuid().ToString() : projectName;
            var projectFullPath = Path.Combine(SolutionDirectory, projectName);
            Directory.CreateDirectory(projectFullPath);

            projectTargetFramework = projectTargetFramework ?? NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext(),
                projectFullPath, projectName);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolder, projectFullPath);
            NuGetProjects.Add(msBuildNuGetProject);
            return msBuildNuGetProject;
        }

        public NuGetProject AddBuildIntegratedProject(string projectName = null, NuGetFramework projectTargetFramework = null)
        {
            if (GetNuGetProject(projectName) != null)
            {
                throw new ArgumentException("Project with " + projectName + " already exists");
            }

            var packagesFolder = Path.Combine(SolutionDirectory, PackagesFolder);
            projectName = string.IsNullOrEmpty(projectName) ? Guid.NewGuid().ToString() : projectName;
            var projectFullPath = Path.Combine(SolutionDirectory, projectName);
            Directory.CreateDirectory(projectFullPath);
            var projectJsonPath = Path.Combine(projectFullPath, "project.json");
            CreateConfigJson(projectJsonPath);

            projectTargetFramework = projectTargetFramework ?? NuGetFramework.Parse("net46");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext(),
                projectFullPath, projectName);

            var projectFilePath = Path.Combine(projectFullPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
            NuGetProject nuGetProject = new BuildIntegratedNuGetProject(projectJsonPath, projectFilePath, msBuildNuGetProjectSystem);
            NuGetProjects.Add(nuGetProject);
            return nuGetProject;
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static JObject BasicConfig
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net46"] = new JObject();

                var deps = new JObject();
                var prop = new JProperty("entityframework", "7.0.0-beta-*");
                deps.Add(prop);

                json["dependencies"] = deps;

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"win-anycpu\": { } }"));

                return json;
            }
        }

        public NuGetProject DefaultNuGetProject
        {
            get { return NuGetProjects.FirstOrDefault(); }
        }

        public string DefaultNuGetProjectName
        {
            get { return DefaultNuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name); }
            set { throw new NotImplementedException(); }
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            return NuGetProjects.
                Where(p => string.Equals(nuGetProjectSafeName, p.GetMetadata<string>(NuGetProjectMetadataKeys.Name), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            return nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
        }

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            return NuGetProjects;
        }

        public bool IsSolutionOpen
        {
            get { return NuGetProjects.Count > 0; }
        }

        public bool IsSolutionAvailable
        {
            get { return IsSolutionOpen; }
        }

#pragma warning disable 0067
        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;

        public event EventHandler<NuGetProjectEventArgs> AfterNuGetProjectRenamed;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public event EventHandler SolutionOpened;
        public event EventHandler SolutionOpening;
        public event EventHandler<ActionsExecutedEventArgs> ActionsExecuted;

        public void OnActionsExecuted(IEnumerable<ResolvedAction> actions)
        {
            if (ActionsExecuted != null)
            {
                ActionsExecuted(this, new ActionsExecutedEventArgs(actions));
            }
        }

        public void SaveProject(NuGetProject nuGetProject)
        {
            //do nothing.
        }

        public void Dispose()
        {
            var testDirectory = _testDirectory;
            if (testDirectory != null)
            {
                testDirectory.Dispose();
                _testDirectory = null;
            }
        }
#pragma warning restore 0067
    }
}
