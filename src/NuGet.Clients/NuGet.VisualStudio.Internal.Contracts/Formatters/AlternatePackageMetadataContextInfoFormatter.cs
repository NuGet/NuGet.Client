// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class AlternatePackageMetadataContextInfoFormatter : IMessagePackFormatter<AlternatePackageMetadataContextInfo?>
    {
        private const string PackageIdPropertyName = "packageid";
        private const string RangePropertyName = "range";

        internal static readonly IMessagePackFormatter<AlternatePackageMetadataContextInfo?> Instance = new AlternatePackageMetadataContextInfoFormatter();

        private AlternatePackageMetadataContextInfoFormatter()
        {
        }

        public AlternatePackageMetadataContextInfo? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? packageId = null;
                VersionRange? range = null;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case PackageIdPropertyName:
                            packageId = reader.ReadString();
                            break;
                        case RangePropertyName:
                            range = VersionRangeFormatter.Instance.Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNullOrEmpty(packageId);
                Assumes.NotNull(range);

                return new AlternatePackageMetadataContextInfo(packageId, range);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, AlternatePackageMetadataContextInfo? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(PackageIdPropertyName);
            writer.Write(value.PackageId);
            writer.Write(RangePropertyName);
            VersionRangeFormatter.Instance.Serialize(ref writer, value.Range, options);
        }
    }
}
