// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal sealed class PackageDependencyGroupFormatter : IMessagePackFormatter<PackageDependencyGroup?>
    {
        private const string TargetFrameworkPropertyName = "targetframework";
        private const string PackagesPropertyName = "packages";

        internal static readonly IMessagePackFormatter<PackageDependencyGroup?> Instance = new PackageDependencyGroupFormatter();

        private PackageDependencyGroupFormatter()
        {
        }

        public PackageDependencyGroup? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                NuGetFramework? framework = null;
                IEnumerable<PackageDependency>? packageDependencies = null;

                int propertyCount = reader.ReadMapHeader();

                for (var propertyIndex = 0; propertyIndex < propertyCount; ++propertyIndex)
                {
                    switch (reader.ReadString())
                    {
                        case TargetFrameworkPropertyName:
                            framework = NuGetFrameworkFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        case PackagesPropertyName:
                            packageDependencies = options.Resolver.GetFormatter<IEnumerable<PackageDependency>>().Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNull(framework);
                Assumes.NotNull(packageDependencies);

                return new PackageDependencyGroup(framework, packageDependencies);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageDependencyGroup? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(TargetFrameworkPropertyName);
            NuGetFrameworkFormatter.Instance.Serialize(ref writer, value.TargetFramework, options);
            writer.Write(PackagesPropertyName);
            options.Resolver.GetFormatter<IEnumerable<PackageDependency>>().Serialize(ref writer, value.Packages, options);
        }
    }
}
