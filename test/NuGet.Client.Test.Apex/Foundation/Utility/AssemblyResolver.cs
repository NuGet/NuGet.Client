using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGetClient.Test.Foundation.Utility
{
    public static class AssemblyResolver
    {
        public static Assembly ResolveAssembly(string[] searchPaths, string assemblyName)
        {
            string name = assemblyName;
            int commaIndex = name.IndexOf(',');
            if (commaIndex >= 0)
            {
                name = name.Substring(0, commaIndex);
            }

            foreach (string searchPath in searchPaths)
            {
                Assembly assembly = FindAndLoadAssembly(searchPath, name);
                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
        }

        private static Assembly FindAndLoadAssembly(string pathToSearch, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(pathToSearch)
                || string.IsNullOrWhiteSpace(assemblyName)
                || !Directory.Exists(pathToSearch))
            {
                return null;
            }

            string fileToLoad = Directory.GetFiles(pathToSearch, "*", SearchOption.AllDirectories).Where(file => Path.GetFileNameWithoutExtension(file).Equals(assemblyName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(fileToLoad))
            {
                return Assembly.LoadFrom(fileToLoad);
            }

            return null;
        }
    }
}

