using System;
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
            builder.Id = assemblyMetadata.Name;
            builder.Version = NuGetVersion.Parse(assemblyMetadata.Version);
            builder.Title = assemblyMetadata.Title;
            builder.Description = assemblyMetadata.Description;
            builder.Copyright = assemblyMetadata.Copyright;

            if (!builder.Authors.Any() && !String.IsNullOrEmpty(assemblyMetadata.Company))
            {
                builder.Authors.Add(assemblyMetadata.Company);
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

                    return new AssemblyMetadata
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
