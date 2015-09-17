// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Build.Utilities;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class FrameworkAssemblyResolver
    {
        // (dotNetFrameworkVersion + dotNetFrameworkProfile) is the key
        private static readonly ConcurrentDictionary<string, List<AssemblyName>> FrameworkAssembliesDictionary = new ConcurrentDictionary<string, List<AssemblyName>>();
        private const string NETFrameworkIdentifier = ".NETFramework";
        internal const string FrameworkListFileName = "RedistList\\FrameworkList.xml";

        /// <summary>
        /// This function checks if there is a framework assembly of assemblyName and of version > availableVersion
        /// in targetFramework. NOTE that this function is only applicable for .NETFramework and returns false for
        /// all the other targetFrameworks
        /// </summary>
        public static bool IsHigherAssemblyVersionInFramework(string simpleAssemblyName, Version availableVersion, FrameworkName targetFrameworkName)
        {
            return IsHigherAssemblyVersionInFramework(simpleAssemblyName, availableVersion, targetFrameworkName,
                ToolLocationHelper.GetPathToReferenceAssemblies, FrameworkAssembliesDictionary);
        }

        /// <summary>
        /// This function checks if there is a framework assembly of assemblyName and of version > availableVersion
        /// in targetFramework. NOTE that this function is only applicable for .NETFramework and returns false for
        /// all the other targetFrameworks
        /// </summary>
        internal static bool IsHigherAssemblyVersionInFramework(string simpleAssemblyName,
            Version availableVersion,
            FrameworkName targetFrameworkName,
            Func<FrameworkName, IList<string>> getPathToReferenceAssembliesFunc,
            ConcurrentDictionary<string, List<AssemblyName>> frameworkAssembliesDictionary)
        {
            if (!String.Equals(targetFrameworkName.Identifier, NETFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string dotNetFrameworkVersion = targetFrameworkName.Version + targetFrameworkName.Profile;

            if (!frameworkAssembliesDictionary.ContainsKey(dotNetFrameworkVersion))
            {
                IList<string> frameworkListFiles = getPathToReferenceAssembliesFunc(targetFrameworkName);
                List<AssemblyName> frameworkAssemblies = GetFrameworkAssemblies(frameworkListFiles);
                frameworkAssembliesDictionary.AddOrUpdate(dotNetFrameworkVersion, frameworkAssemblies, (d, f) => frameworkAssemblies);
            }

            // Find a frameworkAssembly with the same name as assemblyName. If one exists, see if its version is greater than that of the availableversion
            return frameworkAssembliesDictionary[dotNetFrameworkVersion].Any(p => (String.Equals(p.Name, simpleAssemblyName, StringComparison.OrdinalIgnoreCase) && p.Version > availableVersion));
        }

        /// <summary>
        /// Returns the list of framework assemblies as specified in frameworklist.xml under the ReferenceAssemblies
        /// for .NETFramework. If the file is not present, an empty list is returned
        /// </summary>
        private static List<AssemblyName> GetFrameworkAssemblies(IList<string> pathToFrameworkListFiles)
        {
            List<AssemblyName> frameworkAssemblies = new List<AssemblyName>();
            foreach (var pathToFrameworkListFile in pathToFrameworkListFiles)
            {
                if (!String.IsNullOrEmpty(pathToFrameworkListFile))
                {
                    frameworkAssemblies.AddRange(GetFrameworkAssemblies(pathToFrameworkListFile));
                }
            }

            return frameworkAssemblies;
        }

        /// <summary>
        /// Given a fileSystem to the path containing RedistList\Frameworklist.xml file
        /// return the list of assembly names read out from the xml file
        /// </summary>
        internal static List<AssemblyName> GetFrameworkAssemblies(string pathToFrameworkListFile)
        {
            List<AssemblyName> frameworkAssemblies = new List<AssemblyName>();
            try
            {
                if (FileSystemUtility.FileExists(pathToFrameworkListFile, FrameworkListFileName))
                {
                    using (Stream stream = File.OpenRead(FileSystemUtility.GetFullPath(pathToFrameworkListFile, FrameworkListFileName)))
                    {
                        var document = XmlUtility.LoadSafe(stream);
                        var root = document.Root;
                        if (root.Name.LocalName.Equals("FileList", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var element in root.Elements("File"))
                            {
                                string simpleAssemblyName = element.GetOptionalAttributeValue("AssemblyName");
                                string version = element.GetOptionalAttributeValue("Version");
                                if (simpleAssemblyName == null
                                    || version == null)
                                {
                                    // Skip this file. Return an empty list
                                    // Clear frameworkAssemblies since we don't want partial results
                                    frameworkAssemblies.Clear();
                                    break;
                                }
                                AssemblyName assemblyName = new AssemblyName();
                                assemblyName.Name = simpleAssemblyName;
                                assemblyName.Version = new Version(version);
                                frameworkAssemblies.Add(assemblyName);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return frameworkAssemblies;
        }
    }
}
