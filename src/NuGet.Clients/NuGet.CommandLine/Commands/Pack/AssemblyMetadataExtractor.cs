﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGet.CommandLine
{
    using NuGet.Packaging;
    using NuGet.Runtime;
    using NuGet.Versioning;

    public static class AssemblyMetadataExtractor
    {
        public static AssemblyMetadata GetMetadata(string assemblyPath)
        {
            var setup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
            };

            AppDomain domain = AppDomain.CreateDomain("metadata", AppDomain.CurrentDomain.Evidence, setup);
            try
            {
                var extractor = domain.CreateInstance<MetadataExtractor>();
                return extractor.GetMetadata(assemblyPath);
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }

        public static void ExtractMetadata(PackageBuilder builder, string assemblyPath)
        {
            AssemblyMetadata assemblyMetadata = GetMetadata(assemblyPath);
            builder.Version = NuGetVersion.Parse(assemblyMetadata.Version);
            builder.Title = assemblyMetadata.Title;
            builder.Description = assemblyMetadata.Description;
            builder.Copyright = assemblyMetadata.Copyright;

            if (!builder.Authors.Any())
            {
                if (assemblyMetadata.Properties.ContainsKey("authors"))
                {
                    builder.Authors.AddRange(assemblyMetadata.Properties["authors"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                }
                else if (!String.IsNullOrEmpty(assemblyMetadata.Company))
                {
                    builder.Authors.Add(assemblyMetadata.Company);
                }
            }

            if (assemblyMetadata.Properties.ContainsKey("owners"))
            {
                builder.Owners.AddRange(assemblyMetadata.Properties["owners"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            }

            builder.Properties.AddRange(assemblyMetadata.Properties);
            // Let the id be overriden by AssemblyMetadataAttribute
            // This preserves the existing behavior if no id metadata 
            // is provided by the assembly.
            if (builder.Properties.ContainsKey("id"))
            {
                builder.Id = builder.Properties["id"];
            }
            else
            {
                builder.Id = assemblyMetadata.Name;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "It's constructed using CreateInstanceAndUnwrap in another app domain")]
        private sealed class MetadataExtractor : MarshalByRefObject
        {

            private class AssemblyResolver
            {
                private readonly string _lookupPath;

                public AssemblyResolver(string assemblyPath)
                {
                    _lookupPath = Path.GetDirectoryName(assemblyPath);
                }

                public Assembly ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
                {
                    var name = new AssemblyName(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
                    var assemblyPath = Path.Combine(_lookupPath, name.Name + ".dll");
                    return File.Exists(assemblyPath) ? 
                        Assembly.ReflectionOnlyLoadFrom(assemblyPath) : // load from same folder as parent assembly
                        Assembly.ReflectionOnlyLoad(name.FullName);     // load from GAC
                }
            }

            [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "It's a marshal by ref object used to collection information in another app domain")]
            public AssemblyMetadata GetMetadata(string path)
            {
                var resolver = new AssemblyResolver(path);
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver.ReflectionOnlyAssemblyResolve;

                try
                {
                    Assembly assembly = Assembly.ReflectionOnlyLoadFrom(path);
                    AssemblyName assemblyName = assembly.GetName();

                    var attributes = CustomAttributeData.GetCustomAttributes(assembly);

                    NuGetVersion version;
                    string assemblyInformationalVersion = GetAttributeValueOrDefault<AssemblyInformationalVersionAttribute>(attributes);
                    if (!NuGetVersion.TryParse(assemblyInformationalVersion, out version))
                    {
                        if (string.IsNullOrEmpty(assemblyInformationalVersion))
                        {
                            version = NuGetVersion.Parse(assemblyName.Version.ToString());
                        }
                        else
                        {
                            throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.Error_AssemblyInformationalVersion, assemblyInformationalVersion, path));
                        }
                    }

                    return new AssemblyMetadata(GetProperties(attributes))
                    {
                        Name = assemblyName.Name,
                        Version = version.ToString(),
                        Title = GetAttributeValueOrDefault<AssemblyTitleAttribute>(attributes),
                        Company = GetAttributeValueOrDefault<AssemblyCompanyAttribute>(attributes),
                        Description = GetAttributeValueOrDefault<AssemblyDescriptionAttribute>(attributes),
                        Copyright = GetAttributeValueOrDefault<AssemblyCopyrightAttribute>(attributes)
                    };
                }
                finally
                {
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolver.ReflectionOnlyAssemblyResolve;
                }
            }

            private static Dictionary<string, string> GetProperties(IList<CustomAttributeData> attributes)
            {
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                // NOTE: we make this check only by attribute type fullname, and we try to duck
                // type it, therefore enabling the same metadata extesibility behavior for other platforms
                // that don't define the attribute already as part of the framework. 
                // A package author could simply declare this attribute in his own project, using 
                // the same namespace and members, and we'd pick it up automatically. This is consistent 
                // with what MS did in the past with the System.Runtime.CompilerServices.ExtensionAttribute 
                // which allowed Linq to be re-implemented for .NET 2.0 :).
                var attributeName = typeof(AssemblyMetadataAttribute).FullName;
                foreach (var attribute in attributes.Where(x =>
                    x.Constructor.DeclaringType.FullName == attributeName &&
                    x.ConstructorArguments.Count == 2))
                {
                    string key = attribute.ConstructorArguments[0].Value.ToString();
                    string value = attribute.ConstructorArguments[1].Value.ToString();
                    // Return the value only if it isn't null or empty so that we can use ?? to fall back
                    if (!String.IsNullOrEmpty(key) && !String.IsNullOrEmpty(value))
                    {
                        properties[key] = value;
                    }
                }

                return properties;
            }

            private static string GetAttributeValueOrDefault<T>(IList<CustomAttributeData> attributes) where T : Attribute
            {
                foreach (var attribute in attributes)
                {
                    if (attribute.Constructor.DeclaringType == typeof(T))
                    {
                        string value = attribute.ConstructorArguments[0].Value.ToString();
                        // Return the value only if it isn't null or empty so that we can use ?? to fall back
                        if (!String.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
                return null;
            }
        }
    }
}
