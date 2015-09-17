using System;
using System.IO;
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

        // msbuildDirectory is the directory containing the msbuild to be used. E.g. C:\Program Files (x86)\MSBuild\14.0\Bin
        public void LoadAssemblies(string msbuildDirectory)
        {
            if (String.IsNullOrEmpty(msbuildDirectory))
            {
                throw new ArgumentNullException(nameof(msbuildDirectory));
            }

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
    }
}