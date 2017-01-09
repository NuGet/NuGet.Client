using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGet.Common
{
    public abstract class MSBuildUser
    {
        // the Microsoft.Build.dll assembly
        protected Assembly _msbuildAssembly;

        // the Microsoft.Build.Framework.dll assembly
        protected Assembly _frameworkAssembly;

        // The type of class Microsoft.Build.Evaluation.Project
        protected Type _projectType;

        // The type of class Microsoft.Build.Evaluation.ProjectCollection
        protected Type _projectCollectionType;

        protected string _msbuildDirectory;

        // msbuildDirectory is the directory containing the msbuild to be used. E.g. C:\Program Files (x86)\MSBuild\14.0\Bin
        public void LoadAssemblies(string msbuildDirectory)
        {
            if (String.IsNullOrEmpty(msbuildDirectory))
            {
                throw new ArgumentNullException(nameof(msbuildDirectory));
            }

            _msbuildDirectory = msbuildDirectory;
            _msbuildAssembly = Assembly.LoadFile(Path.Combine(msbuildDirectory, "Microsoft.Build.dll"));
            _frameworkAssembly = Assembly.LoadFile(Path.Combine(msbuildDirectory, "Microsoft.Build.Framework.dll"));

            LoadTypes();
        }

        public void LoadTypes()
        {
            _projectType = _msbuildAssembly.GetType(
                "Microsoft.Build.Evaluation.Project",
                throwOnError: true);
            _projectCollectionType = _msbuildAssembly.GetType(
                "Microsoft.Build.Evaluation.ProjectCollection",
                throwOnError: true);
        }

        // This handler is called only when the common language runtime tries to bind to the assembly and fails
        protected Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(_msbuildDirectory))
            {
                return null;
            }

            var failingAssemblyFilename = args.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            // If we're failing to load a resource assembly, we need to find it in the appropriate subdir
            if (failingAssemblyFilename.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                var resourceDir = new[] {
                    Path.Combine(_msbuildDirectory, CultureInfo.CurrentCulture.TwoLetterISOLanguageName),
                    Path.Combine(_msbuildDirectory, "en") }
                    .FirstOrDefault(d => Directory.Exists(d));

                if (resourceDir == null)
                {
                    return null; // no resource directory or fallback-to-en resource directory - fail
                }

                return Assembly.LoadFrom(Path.Combine(resourceDir, failingAssemblyFilename + ".dll"));
            }

            // Non-resource DLL - attempt to load from MSBuild directory
            return Assembly.LoadFrom(Path.Combine(_msbuildDirectory, failingAssemblyFilename + ".dll"));
        }
    }
}