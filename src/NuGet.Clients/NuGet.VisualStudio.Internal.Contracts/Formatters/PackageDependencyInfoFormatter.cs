// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal sealed class PackageDependencyInfoFormatter : IMessagePackFormatter<PackageDependencyInfo?>
    {
        private const string PackageDependenciesPropertyName = "packagedependencies";
        private const string PackageIdentityPropertyName = "packageidentity";

        internal static readonly IMessagePackFormatter<PackageDependencyInfo?> Instance = new PackageDependencyInfoFormatter();

        private PackageDependencyInfoFormatter()
        {
        }

        public PackageDependencyInfo? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                PackageIdentity? packageIdentity = null;
                List<PackageDependency>? packageDependencies = null;

                int propertyCount = reader.ReadMapHeader();

                for (var propertyIndex = 0; propertyIndex < propertyCount; ++propertyIndex)
                {
                    switch (reader.ReadString())
                    {
                        case PackageIdentityPropertyName:
                            packageIdentity = PackageIdentityFormatter.Instance.Deserialize(ref reader, options);
                            break;

                        case PackageDependenciesPropertyName:
                            packageDependencies = new List<PackageDependency>();

                            int dependenciesCount = reader.ReadArrayHeader();

                            for (var i = 0; i < dependenciesCount; ++i)
                            {
                                PackageDependency? packageDependency = PackageDependencyFormatter.Instance.Deserialize(ref reader, options);

                                Assumes.NotNull(packageDependency);

                                packageDependencies.Add(packageDependency);
                            }
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNull(packageIdentity);

                return new PackageDependencyInfo(packageIdentity, packageDependencies);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageDependencyInfo? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(PackageIdentityPropertyName);
            PackageIdentityFormatter.Instance.Serialize(ref writer, value, options);
            writer.Write(PackageDependenciesPropertyName);
            writer.WriteArrayHeader(value.Dependencies.Count());

            foreach (PackageDependency packageDependency in value.Dependencies)
            {
                PackageDependencyFormatter.Instance.Serialize(ref writer, packageDependency, options);
            }
        }
    }
}
