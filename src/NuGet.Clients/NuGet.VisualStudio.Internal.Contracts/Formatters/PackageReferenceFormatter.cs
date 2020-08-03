// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public class PackageReferenceInfo2 : PackageReference
    {
        public PackageReferenceInfo2(PackageIdentity identity, NuGetFramework targetFramework) : base(identity, targetFramework)
        {
        }

        public bool IsLibraryDependencyAutoReferenced { get; set; }
    }

    public sealed class PackageReferenceInfo
    {
        public PackageReferenceInfo(PackageIdentity identity, NuGetFramework targetFramework)
        {
            Identity = identity;
            TargetFramework = targetFramework;
        }

        public bool IsLibraryDependencyAutoReferenced { get; set; }
        public PackageIdentity Identity { get; }
        public NuGetFramework TargetFramework { get; }
    }

    internal class PackageReferenceFormatter : IMessagePackFormatter<PackageReference?>
    {
        private const string PackageIdentityPropertyName = "packageidentity";
        private const string TargetFrameworkPropertyName = "targetframework";

        internal static readonly IMessagePackFormatter<PackageReference?> Instance = new PackageReferenceFormatter();

        private PackageReferenceFormatter()
        {
        }

        public PackageReference? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                PackageIdentity? identity = null;
                NuGetFramework? framework = null;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case PackageIdentityPropertyName:
                            identity = PackageIdentityFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        case TargetFrameworkPropertyName:
                            framework = NuGetFrameworkFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNull(identity);
                Assumes.NotNull(framework);

                return new PackageReference(identity, framework);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageReference? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            int mapHeaderObjectsToWrite = 2;

            //if (value is BuildIntegratedPackageReference buildIntegratedPackageReference && buildIntegratedPackageReference.Dependency != null)
            //{
            //    mapHeaderObjectsToWrite++;

            //    writer.WriteMapHeader(mapHeaderObjectsToWrite);
            //    writer.Write("autoreferenced");
            //    writer.Write(buildIntegratedPackageReference.Dependency.AutoReferenced);
            //}
            //else
            {
                writer.WriteMapHeader(mapHeaderObjectsToWrite);
            }

            //writer.Write("typefullname");
            //writer.Write(value.GetType().FullName);
            writer.Write(PackageIdentityPropertyName);
            PackageIdentityFormatter.Instance.Serialize(ref writer, value.PackageIdentity, options);
            writer.Write(TargetFrameworkPropertyName);
            NuGetFrameworkFormatter.Instance.Serialize(ref writer, value.TargetFramework, options);
        }
    }
}
