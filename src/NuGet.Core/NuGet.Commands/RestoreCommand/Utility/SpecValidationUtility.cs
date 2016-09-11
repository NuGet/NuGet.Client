using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public static class SpecValidationUtility
    {
        /// <summary>
        /// Validate a dg file. This will throw a RestoreSpecException if there are errors.
        /// </summary>
        public static void ValidateDependencySpec(DependencyGraphSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            try
            {
                // Verify projects
                foreach (var projectSpec in spec.Projects)
                {
                    ValidateProjectSpec(projectSpec);
                }

                var restoreSet = new HashSet<string>(spec.Restore, StringComparer.Ordinal);
                var projectSet = new HashSet<string>(
                    spec.Projects.Select(p => p.RestoreMetadata?.ProjectUniqueName)
                    .Where(s => !string.IsNullOrEmpty(s)),
                    StringComparer.Ordinal);

                // Verify restore does not reference a project that does not exist
                foreach (var missing in restoreSet.Except(projectSet))
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.SpecValidationMissingProject,
                        missing);

                    throw RestoreSpecException.Create(message, Enumerable.Empty<string>());
                }

                // Verify restore contains at least one project
                if (restoreSet.Count < 1)
                {
                    throw RestoreSpecException.Create(Strings.SpecValidationZeroRestoreRequests, Enumerable.Empty<string>());
                }
            }
            catch (Exception ex) when (!(ex is RestoreSpecException))
            {
                // Catch and wrap any unexpected exceptions
                throw RestoreSpecException.Create(
                    ex.Message,
                    Enumerable.Empty<string>(),
                    ex);
            }
        }

        public static void ValidateProjectSpec(PackageSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // Track the spec path
            var files = new Stack<string>();
            files.Push(spec.FilePath);

            // restore metadata must exist for all project types
            var restoreMetadata = spec.RestoreMetadata;

            if (restoreMetadata == null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.MissingRequiredProperty, nameof(spec.RestoreMetadata));

                throw RestoreSpecException.Create(message, files);
            }

            // Track the project path
            files.Push(restoreMetadata.ProjectPath);

            var outputType = spec.RestoreMetadata?.OutputType;

            // Verify project metadata
            ValidateProjectMetadata(spec, files);

            // Verify frameworks
            ValidateFrameworks(spec, files);

            // Verify project references
            ValidateProjectReferences(spec, files);

            // Verify based on the type.
            switch (outputType)
            {
                case RestoreOutputType.NETCore:
                    ValidateProjectSpecNetCore(spec, files);
                    break;

                case RestoreOutputType.UAP:
                    ValidateProjectSpecUAP(spec, files);
                    break;

                default:
                    ValidateProjectSpecOther(spec, files);
                    break;
            }
        }

        private static void ValidateFrameworks(PackageSpec spec, IEnumerable<string> files)
        {
            var frameworks = spec.TargetFrameworks.Select(f => f.FrameworkName).ToArray();

            // Verify frameworks are valid
            foreach (var framework in frameworks.Where(f => !f.IsSpecificFramework))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.SpecValidationInvalidFramework,
                    framework.GetShortFolderName());

                throw RestoreSpecException.Create(message, files);
            }

            // Must have at least 1 framework
            if (frameworks.Length < 1)
            {
                throw RestoreSpecException.Create(Strings.SpecValidationNoFrameworks, files);
            }

            // Duplicate frameworks may not exist
            if (frameworks.Length != frameworks.Distinct().Count())
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.SpecValidationDuplicateFrameworks,
                    string.Join(", ", frameworks.Select(f => f.GetShortFolderName())));

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static void ValidateProjectSpecNetCore(PackageSpec spec, IEnumerable<string> files)
        {
            // NETCore may not specify a project.json file
            if (!string.IsNullOrEmpty(spec.RestoreMetadata.ProjectJsonPath))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PropertyNotAllowedForProjectType,
                    nameof(spec.RestoreMetadata.ProjectJsonPath),
                    RestoreOutputType.NETCore.ToString());

                throw RestoreSpecException.Create(message, files);
            }

            // Output path must be set for netcore
            if (string.IsNullOrEmpty(spec.RestoreMetadata.OutputPath))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredPropertyForProjectType,
                    nameof(spec.RestoreMetadata.OutputPath),
                    RestoreOutputType.NETCore.ToString());

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static void ValidateProjectSpecUAP(PackageSpec spec, IEnumerable<string> files)
        {
            // UAP may contain only 1 framework
            if (spec.TargetFrameworks.Count != 1)
            {
                throw RestoreSpecException.Create(Strings.SpecValidationUAPSingleFramework, files);
            }

            // UAP must specify a project.json file
            if (string.IsNullOrEmpty(spec.RestoreMetadata.ProjectJsonPath)
                || spec.RestoreMetadata.ProjectJsonPath != spec.FilePath)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredPropertyForProjectType,
                    nameof(spec.RestoreMetadata.ProjectJsonPath),
                    RestoreOutputType.UAP.ToString());

                throw RestoreSpecException.Create(message, files);
            }

            // Do not allow changing the output path for UAP
            if (!string.IsNullOrEmpty(spec.RestoreMetadata.OutputPath))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PropertyNotAllowedForProjectType,
                    nameof(spec.RestoreMetadata.OutputPath),
                    RestoreOutputType.UAP.ToString());

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static void ValidateProjectSpecOther(PackageSpec spec, IEnumerable<string> files)
        {
            // Unknown project types may not have a project.json path
            if (spec.RestoreMetadata.ProjectJsonPath != null)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PropertyNotAllowed,
                    nameof(spec.RestoreMetadata.ProjectJsonPath));

                throw RestoreSpecException.Create(message, files);
            }

            // Unknown project types may not carry package dependencies
            var packageDependencies = GetAllDependencies(spec)
                .Where(d => d.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package));

            if (packageDependencies.Any())
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PropertyNotAllowed,
                    nameof(spec.Dependencies));

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static void ValidateProjectReferences(PackageSpec spec, IEnumerable<string> files)
        {
            var dependencies = new HashSet<string>(GetAllDependencies(spec)
                .Where(d => d.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.ExternalProject))
                .Select(d => d.Name),
                StringComparer.OrdinalIgnoreCase);

            var projectOnly = new HashSet<string>(GetAllDependencies(spec)
                .Where(d => d.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.ExternalProject)
                        && !d.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                        && !d.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Reference))
                .Select(d => d.Name),
                StringComparer.OrdinalIgnoreCase);

            var externalReferences = new HashSet<string>(
                spec.RestoreMetadata.ProjectReferences.Select(p => p.ProjectUniqueName),
                StringComparer.OrdinalIgnoreCase);

            foreach (var missing in externalReferences.Except(dependencies))
            {
                // Missing dependency in dependencies section
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.SpecValidationMissingDependency,
                    missing);

                throw RestoreSpecException.Create(message, files);
            }

            foreach (var missing in projectOnly.Except(dependencies))
            {
                // missing restore section reference containing project path
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.SpecValidationMissingDependency,
                    missing);

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static void ValidateProjectMetadata(PackageSpec spec, IEnumerable<string> files)
        {
            // spec file path must be set
            if (string.IsNullOrEmpty(spec.FilePath))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredProperty,
                    nameof(spec.FilePath));

                throw RestoreSpecException.Create(message, files);
            }

            // spec name must be set
            if (string.IsNullOrEmpty(spec.Name))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredProperty,
                    nameof(spec.Name));

                throw RestoreSpecException.Create(message, files);
            }

            // unique name must be set
            if (string.IsNullOrEmpty(spec.RestoreMetadata.ProjectUniqueName))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredProperty,
                    nameof(spec.RestoreMetadata.ProjectUniqueName));

                throw RestoreSpecException.Create(message, files);
            }

            // project name must be set
            if (string.IsNullOrEmpty(spec.RestoreMetadata.ProjectName))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredProperty,
                    nameof(spec.RestoreMetadata.ProjectName));

                throw RestoreSpecException.Create(message, files);
            }

            // msbuild project path must be set
            if (string.IsNullOrEmpty(spec.RestoreMetadata.ProjectPath))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRequiredProperty,
                    nameof(spec.RestoreMetadata.ProjectPath));

                throw RestoreSpecException.Create(message, files);
            }

            // block xproj
            if (spec.RestoreMetadata.ProjectPath.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Error_XPROJNotAllowed,
                    nameof(spec.RestoreMetadata.ProjectPath));

                throw RestoreSpecException.Create(message, files);
            }
        }

        private static IEnumerable<LibraryDependency> GetAllDependencies(PackageSpec spec)
        {
            return spec.Dependencies
                .Concat(spec.TargetFrameworks.SelectMany(f => f.Dependencies));
        }
    }
}