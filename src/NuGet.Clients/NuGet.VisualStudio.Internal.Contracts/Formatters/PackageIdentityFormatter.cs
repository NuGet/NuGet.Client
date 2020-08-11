// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class PackageIdentityFormatter : IMessagePackFormatter<PackageIdentity?>
    {
        private const string NuGetVersionPropertyName = "version";
        private const string IdPropertyName = "id";

        internal static readonly IMessagePackFormatter<PackageIdentity?> Instance = new PackageIdentityFormatter();

        private PackageIdentityFormatter()
        {
        }

        public PackageIdentity? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? id = null;
                NuGetVersion? version = null;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case IdPropertyName:
                            id = reader.ReadString();
                            break;
                        case NuGetVersionPropertyName:
                            version = NuGetVersionFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new PackageIdentity(id, version);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageIdentity? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(IdPropertyName);
            writer.Write(value.Id);
            writer.Write(NuGetVersionPropertyName);
            NuGetVersionFormatter.Instance.Serialize(ref writer, value.Version, options);
        }
    }
}
