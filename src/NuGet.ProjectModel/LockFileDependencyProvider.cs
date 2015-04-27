using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
                IEnumerable<LibraryDependency> dependencies;
                bool resolved = true;
                var frameworkGroup = library.FrameworkGroups.FirstOrDefault(g => g.TargetFramework.Equals(targetFramework));
                if (frameworkGroup == null)
                {
                    // Library does not exist for this target framework
                    dependencies = Enumerable.Empty<LibraryDependency>();
                    resolved = false;
                }
                else
                {
                    dependencies = GetDependencies(frameworkGroup);
                }

                var description = new Library
                {
                    LibraryRange = libraryRange,
                    Identity = new LibraryIdentity
                    {
                        Name = library.Name,
                        Version = library.Version,
                        Type = LibraryTypes.Package
                    },
                    Resolved = resolved,
                    Dependencies = dependencies,

                    [KnownLibraryProperties.LockFileLibrary] = library,
                    [KnownLibraryProperties.LockFileFrameworkGroup] = frameworkGroup
                };

                description.Items[KnownLibraryProperties.LockFileLibrary] = library;
                if (frameworkGroup != null)
                {
                    description.Items[KnownLibraryProperties.LockFileFrameworkGroup] = frameworkGroup;
                }

                return description;
            }

            return null;
        }

        private IList<LibraryDependency> GetDependencies(LockFileFrameworkGroup frameworkGroup)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (frameworkGroup.Dependencies != null)
            {
                foreach (var d in frameworkGroup.Dependencies)
                {
                    libraryDependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = d.Id,
                            VersionRange = d.VersionRange
                        }
                    });
                }
            }

            if (!frameworkGroup.TargetFramework.IsDesktop())
            {
                // REVIEW: This isn't 100% correct since none *can* mean 
                // any in theory, but in practice it means .NET full reference assembly
                // If there's no supported target frameworks and we're not targeting
                // the desktop framework then skip it.

                // To do this properly we'll need all reference assemblies supported
                // by each supported target framework which isn't always available.
                return libraryDependencies;
            }

            foreach (var name in frameworkGroup.FrameworkAssemblies)
            {
                libraryDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = name,
                        TypeConstraint = LibraryTypes.Reference
                    }
                });
            }

            return libraryDependencies;
        }

        private LockFileLibrary FindCandidate(LibraryRange libraryRange)
        {
            var packages = _libraries[libraryRange.Name];
            return packages.FindBestMatch(libraryRange.VersionRange, library => library?.Version);
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            throw new NotImplementedException();
        }
    }
}