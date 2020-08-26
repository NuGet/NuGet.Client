// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// IAssembly implementation that is used for marshalling information across app domains.
    /// </summary>
    internal class RemoteAssembly : MarshalByRefObject, IAssembly
    {
        private static readonly Dictionary<Tuple<string, string>, Assembly> _assemblyCache = new Dictionary<Tuple<string, string>, Assembly>();
        private readonly List<IAssembly> _referencedAssemblies = new List<IAssembly>();

        public string Name { get; private set; }

        public Version Version { get; private set; }

        public string PublicKeyToken { get; private set; }

        public string Culture { get; private set; }

        public IEnumerable<IAssembly> ReferencedAssemblies
        {
            get { return _referencedAssemblies; }
        }

        public void Load(string path)
        {
            // The cache key is the file name plus the full name of the assembly.
            // This is so we don't load the same assembly more than once from different paths
            string fileName = Path.GetFileName(path).ToUpperInvariant();
            var cacheKey = Tuple.Create(fileName, AssemblyName.GetAssemblyName(path).FullName);

            Assembly assembly;
            if (!_assemblyCache.TryGetValue(cacheKey, out assembly))
            {
                // Load the assembly in a reflection only context
                assembly = Assembly.ReflectionOnlyLoadFrom(path);
                _assemblyCache[cacheKey] = assembly;
            }

            // Get the assembly name and set the properties on this object
            CopyAssemblyProperties(assembly.GetName(), this);

            // Do the same for referenced assemblies
            foreach (AssemblyName referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                // Copy the properties to the referenced assembly
                var referencedAssembly = new RemoteAssembly();
                _referencedAssemblies.Add(CopyAssemblyProperties(referencedAssemblyName, referencedAssembly));
            }
        }

        private static RemoteAssembly CopyAssemblyProperties(AssemblyName assemblyName, RemoteAssembly assembly)
        {
            assembly.Name = assemblyName.Name;
            assembly.Version = assemblyName.Version;
            assembly.PublicKeyToken = GetPublicKeyTokenString(assemblyName);
            string culture = assemblyName.CultureInfo.ToString();
            assembly.Culture = String.IsNullOrEmpty(culture) ? "neutral" : culture;

            return assembly;
        }

        internal static IAssembly LoadAssembly(string path, AppDomain domain)
        {
            if (domain != AppDomain.CurrentDomain)
            {
                var crossDomainAssembly = CreateInstance<RemoteAssembly>(domain);
                crossDomainAssembly.Load(path);

                return crossDomainAssembly;
            }

            var assembly = new RemoteAssembly();
            assembly.Load(path);
            return assembly;
        }

        private static string GetPublicKeyTokenString(AssemblyName assemblyName)
        {
            return String.Join(String.Empty, assemblyName.GetPublicKeyToken()
                .Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static T CreateInstance<T>(AppDomain domain)
        {
            return (T)domain.CreateInstanceAndUnwrap(typeof(T).Assembly.FullName,
                typeof(T).FullName);
        }
    }
}
