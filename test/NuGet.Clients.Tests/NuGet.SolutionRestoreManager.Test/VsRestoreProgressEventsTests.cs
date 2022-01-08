// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class VsRestoreProgressEventsTests
    {
        [Fact]
        public void Constructor_EmptyParamConstructor()
        {
            _ = new VsRestoreProgressEvents();
        }

        [Fact]
        public void StartProjectUpdate_WithNullProjectName_ThrowsArgumentNullException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentNullException>(() => restoreProgressEvents.StartProjectUpdate(null, new string[] { }));
        }

        [Fact]
        public void StartProjectUpdate_WithValidProjectName_FiresProjectUpdateEvent()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();

            var expectedProjectName = "projectName.csproj";
            var expectedFileListName = new List<string>() { "project.assets.json" };

            string actualProjectName = null;
            IReadOnlyList<string> actualFileListName = null;

            restoreProgressEvents.ProjectUpdateStarted += (projectUniqueName, updatedFiles) =>
            {
                actualProjectName = projectUniqueName;
                actualFileListName = updatedFiles;
            };

            restoreProgressEvents.StartProjectUpdate(expectedProjectName, expectedFileListName);

            Assert.Equal(expectedProjectName, actualProjectName);
            Assert.Equal(expectedFileListName, actualFileListName);
        }

        [Fact]
        public void EndProjectUpdate_WithNullProjectName_ThrowsArgumentNullException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentNullException>(() => restoreProgressEvents.EndProjectUpdate(null, new string[] { }));
        }

        [Fact]
        public void EndProjectUpdate_WithValidProjectName_FiresProjectUpdateEvent()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();

            var expectedProjectName = "projectName.csproj";
            var expectedFileListName = new List<string>() { "project.assets.json" };

            string actualProjectName = null;
            IReadOnlyList<string> actualFileListName = null;

            restoreProgressEvents.ProjectUpdateFinished += (projectUniqueName, updatedFiles) =>
            {
                actualProjectName = projectUniqueName;
                actualFileListName = updatedFiles;
            };

            restoreProgressEvents.EndProjectUpdate(expectedProjectName, expectedFileListName);

            Assert.Equal(expectedProjectName, actualProjectName);
            Assert.Equal(expectedFileListName, actualFileListName);
        }


        [Fact]
        public void StartSolutionRestore_WithNullProjectList_ThrowsArgumentException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentException>(() => restoreProgressEvents.StartSolutionRestore(null));
        }

        [Fact]
        public void StartSolutionRestore_WithEmptyProjectList_ThrowsArgumentException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentException>(() => restoreProgressEvents.StartSolutionRestore(new List<string>()));
        }

        [Fact]
        public void StartSolutionRestore_WithValidProjectName_FiresProjectUpdateEvent()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();

            var expectedProjectList = new List<string>() { "projectName.csproj" };

            IReadOnlyList<string> actualProjectList = null;

            restoreProgressEvents.SolutionRestoreStarted += (updatedFiles) =>
            {
                actualProjectList = updatedFiles;
            };

            restoreProgressEvents.StartSolutionRestore(expectedProjectList);

            Assert.Equal(expectedProjectList, actualProjectList);
        }


        [Fact]
        public void EndSolutionRestore_WithNullProjectList_ThrowsArgumentException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentException>(() => restoreProgressEvents.EndSolutionRestore(null));
        }

        [Fact]
        public void EndSolutionRestore_WithEmptyProjectList_ThrowsArgumentException()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();
            Assert.Throws<ArgumentException>(() => restoreProgressEvents.EndSolutionRestore(new List<string>()));
        }

        [Fact]
        public void EndSolutionRestore_WithValidProjectName_FiresProjectUpdateEvent()
        {
            var restoreProgressEvents = new VsRestoreProgressEvents();

            var expectedProjectList = new List<string>() { "projectName.csproj" };

            IReadOnlyList<string> actualProjectList = null;

            restoreProgressEvents.SolutionRestoreFinished += (updatedFiles) =>
            {
                actualProjectList = updatedFiles;
            };

            restoreProgressEvents.EndSolutionRestore(expectedProjectList);

            Assert.Equal(expectedProjectList, actualProjectList);
        }
    }
}
