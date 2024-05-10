using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGet.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class MSBuildUser
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // the Microsoft.Build.dll assembly
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Assembly _msbuildAssembly;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        // the Microsoft.Build.Framework.dll assembly
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Assembly _frameworkAssembly;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        // The type of class Microsoft.Build.Evaluation.Project
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Type _projectType;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        // The type of class Microsoft.Build.Evaluation.ProjectCollection
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Type _projectCollectionType;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected string _msbuildDirectory;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        // msbuildDirectory is the directory containing the msbuild to be used. E.g. C:\Program Files (x86)\MSBuild\15.0\Bin
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void LoadAssemblies(string msbuildDirectory)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (string.IsNullOrEmpty(msbuildDirectory))
            {
                throw new ArgumentNullException(nameof(msbuildDirectory));
            }

            string microsoftBuildDllPath = Path.Combine(msbuildDirectory, "Microsoft.Build.dll");

            if (!File.Exists(microsoftBuildDllPath))
            {
                throw new FileNotFoundException(message: null, microsoftBuildDllPath);
            }

            _msbuildDirectory = msbuildDirectory;
            _msbuildAssembly = Assembly.LoadFrom(microsoftBuildDllPath);
            _frameworkAssembly = Assembly.LoadFrom(Path.Combine(msbuildDirectory, "Microsoft.Build.Framework.dll"));

            LoadTypes();
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void LoadTypes()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            _projectType = _msbuildAssembly.GetType(
                "Microsoft.Build.Evaluation.Project",
                throwOnError: true);
            _projectCollectionType = _msbuildAssembly.GetType(
                "Microsoft.Build.Evaluation.ProjectCollection",
                throwOnError: true);
        }

        // This handler is called only when the common language runtime tries to bind to the assembly and fails
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected Assembly AssemblyResolve(object sender, ResolveEventArgs args)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (string.IsNullOrEmpty(_msbuildDirectory))
            {
                return null;
            }

            var failingAssemblyFilename = args
                .Name
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            var assemblyPath = string.Empty;
            var resourceDir = string.Empty;

            // If we're failing to load a resource assembly, we need to find it in the appropriate subdir
            if (failingAssemblyFilename.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                resourceDir = new[] {
                    Path.Combine(_msbuildDirectory, CultureInfo.CurrentCulture.TwoLetterISOLanguageName),
                    Path.Combine(_msbuildDirectory, "en") }
                    .FirstOrDefault(d => Directory.Exists(d));
            }
            else
            {
                // Non-resource DLL - attempt to load from MSBuild directory
                resourceDir = _msbuildDirectory;
            }

            if (string.IsNullOrEmpty(resourceDir))
            {
                return null; // no resource directory or fallback-to-en resource directory - fail
            }

            assemblyPath = Path.Combine(resourceDir, failingAssemblyFilename + ".dll");

            if (!File.Exists(assemblyPath))
            {
                return null; // no dll present - fail
            }

            return Assembly.LoadFrom(assemblyPath);
        }
    }
}
