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

            return GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        // TODO: Figure out how to share this
        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
                                                                PackageDependencyGroup dependencies,
                                                                FrameworkSpecificGroup frameworkAssemblies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                foreach (var d in dependencies.Packages)
                {
                    libraryDependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = d.Id,
                            VersionRange = d.VersionRange == null ? null : d.VersionRange
                        }
                    });
                }
            }

            if (frameworkAssemblies == null)
            {
                return libraryDependencies;
            }

            if (frameworkAssemblies.TargetFramework.AnyPlatform && !targetFramework.IsDesktop())
            {
                // REVIEW: This isn't 100% correct since none *can* mean 
                // any in theory, but in practice it means .NET full reference assembly
                // If there's no supported target frameworks and we're not targeting
                // the desktop framework then skip it.

                // To do this properly we'll need all reference assemblies supported
                // by each supported target framework which isn't always available.
                return libraryDependencies;
            }

            foreach (var name in frameworkAssemblies.Items)
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
    }
}