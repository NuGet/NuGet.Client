using System.Collections.Generic;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileDependencyProvider : IDependencyProvider
    {
        private readonly ILookup<string, LockFileLibrary> _libraries;

        public LockFileDependencyProvider(LockFile lockFile)
        {
            _libraries = lockFile.Libraries.ToLookup(l => l.Name);
        }

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType) ||
                   string.Equals(libraryType, LibraryTypes.Package);
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var library = FindCandidate(libraryRange);

            if (library != null)
            {
                var description = new Library
                {
                    LibraryRange = libraryRange,
                    Identity = new LibraryIdentity
                    {
                        Name = library.Name,
                        Version = library.Version,
                        Type = LibraryTypes.Package
                    },
                    Dependencies = GetDependencies(library, targetFramework)
                };

                description.Items["package"] = library;
                description.Items["files"] = library.Files;

                return description;
            }

            return null;
        }

        private IList<LibraryDependency> GetDependencies(LockFileLibrary library, NuGetFramework targetFramework)
        {
            var dependencies = NuGetFrameworkUtility.GetNearest(library.DependencyGroups,
                                                      targetFramework,
                                                      item => item.TargetFramework);

            var frameworkAssemblies = NuGetFrameworkUtility.GetNearest(library.FrameworkReferenceGroups,
                                                             targetFramework,
                                                             item => item.TargetFramework);

            return NuGetDependencyResolver.GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        private LockFileLibrary FindCandidate(LibraryRange libraryRange)
        {
            var packages = _libraries[libraryRange.Name];
            return packages.FindBestMatch(libraryRange.VersionRange, library => library?.Version);
        }
    }
}