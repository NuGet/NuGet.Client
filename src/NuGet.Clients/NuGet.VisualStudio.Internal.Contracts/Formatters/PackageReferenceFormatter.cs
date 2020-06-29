// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class PackageReferenceFormatter : IMessagePackFormatter<PackageReference?>
    {
        private const string PackageIdentityPropertyName = "packageidentity";
        private const string NuGetFrameworkPropertyName = "nugetframework";

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
                        case NuGetFrameworkPropertyName:
                            framework = NuGetFrameworkFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

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

            writer.WriteMapHeader(2);
            writer.Write(PackageIdentityPropertyName);
            PackageIdentityFormatter.Instance.Serialize(ref writer, value.PackageIdentity, options);
            writer.Write(NuGetFrameworkPropertyName);
            NuGetFrameworkFormatter.Instance.Serialize(ref writer, value.TargetFramework, options);
        }
    }
}
