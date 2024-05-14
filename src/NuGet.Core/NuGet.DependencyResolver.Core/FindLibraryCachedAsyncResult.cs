// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.LibraryModel;

#pragma warning disable RS0016
namespace NuGet.DependencyResolver
{
    public class FindLibraryCachedAsyncResult
    {
        private LibraryDependencyIndex[] _dependencyIndices;
        private LibraryRangeIndex[] _rangeIndices;

        public FindLibraryCachedAsyncResult(
            LibraryDependency libraryDependency,
            GraphItem<RemoteResolveResult> resolvedItem,
            LibraryDependencyInterningTable libraryDependencyInterningTable,
            LibraryRangeInterningTable libraryRangeInterningTable)
        {
            Item = resolvedItem;
            DependencyIndex = libraryDependencyInterningTable.Intern(libraryDependency);
            RangeIndex = libraryRangeInterningTable.Intern(libraryDependency.LibraryRange);
            int dependencyCount = resolvedItem.Data.Dependencies.Count;
            _dependencyIndices = new LibraryDependencyIndex[dependencyCount];
            _rangeIndices = new LibraryRangeIndex[dependencyCount];
            for (int i = 0; i < dependencyCount; i++)
            {
                LibraryDependency dependency = resolvedItem.Data.Dependencies[i];
                _dependencyIndices[i] = libraryDependencyInterningTable.Intern(dependency);
                _rangeIndices[i] = libraryRangeInterningTable.Intern(dependency.LibraryRange);
            }
        }

        public GraphItem<RemoteResolveResult> Item { get; }

        public LibraryDependencyIndex DependencyIndex { get; }

        public LibraryRangeIndex RangeIndex { get; }

        public LibraryDependencyIndex GetDependencyIndexForDependency(int dependencyIndex)
        {
            return _dependencyIndices[dependencyIndex];
        }

        public LibraryRangeIndex GetRangeIndexForDependency(int dependencyIndex)
        {
            return _rangeIndices[dependencyIndex];
        }
    }
}
#pragma warning restore
