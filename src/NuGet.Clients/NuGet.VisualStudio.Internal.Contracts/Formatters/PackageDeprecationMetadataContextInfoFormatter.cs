// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class PackageDeprecationMetadataContextInfoFormatter : IMessagePackFormatter<PackageDeprecationMetadataContextInfo?>
    {
        private const string MessagePropertyName = "message";
        private const string ReasonsPropertyName = "reasons";
        private const string AlternatePackageMetadataPropertyName = "alternatepackage";

        internal static readonly IMessagePackFormatter<PackageDeprecationMetadataContextInfo?> Instance = new PackageDeprecationMetadataContextInfoFormatter();

        private PackageDeprecationMetadataContextInfoFormatter()
        {
        }

        public PackageDeprecationMetadataContextInfo? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? message = null;
                IReadOnlyCollection<string>? reasons = null;
                AlternatePackageMetadataContextInfo? alternatePackageMetadataContextInfo = null;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case MessagePropertyName:
                            message = reader.ReadString();
                            break;
                        case ReasonsPropertyName:
                            reasons = reader.TryReadNil() ? null : options.Resolver.GetFormatter<IReadOnlyCollection<string>>().Deserialize(ref reader, options); ;
                            break;
                        case AlternatePackageMetadataPropertyName:
                            alternatePackageMetadataContextInfo = reader.TryReadNil() ? null : options.Resolver.GetFormatter<AlternatePackageMetadataContextInfo>().Deserialize(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNullOrEmpty(message);

                return new PackageDeprecationMetadataContextInfo(message, reasons, alternatePackageMetadataContextInfo);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageDeprecationMetadataContextInfo? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 3);
            writer.Write(MessagePropertyName);
            writer.Write(value.Message);
            writer.Write(ReasonsPropertyName);
            if(value.Reasons == null)
            {
                writer.WriteNil();
            }
            else
            {
                options.Resolver.GetFormatter<IReadOnlyCollection<string>>().Serialize(ref writer, value.Reasons, options);

            }
            writer.Write(AlternatePackageMetadataPropertyName);
            if(value.AlternatePackage == null)
            {
                writer.WriteNil();
            }
            else
            {
                options.Resolver.GetFormatter<AlternatePackageMetadataContextInfo>().Serialize(ref writer, value.AlternatePackage, options);
            }
        }
    }
}
