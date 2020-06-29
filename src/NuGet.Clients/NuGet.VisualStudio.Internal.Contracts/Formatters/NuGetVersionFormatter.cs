// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class NuGetVersionFormatter : IMessagePackFormatter<NuGetVersion?>
    {
        private const string VersionPropertyName = "version";
        private const string MetadataPropertyName = "metadata";
        private const string ReleaseLabelsPropertyName = "releaselabels";
        private const string OriginalVersionPropertyName = "originalversion";

        internal static readonly IMessagePackFormatter<NuGetVersion?> Instance = new NuGetVersionFormatter();

        private NuGetVersionFormatter()
        {
        }

        public NuGetVersion? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                int propertyCount = reader.ReadMapHeader();

                string? versionString = null;
                string? metadata = null;
                string? originalVersion = null;
                IEnumerable<string>? releaseLabels = null;

                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case VersionPropertyName:
                            versionString = reader.ReadString();
                            break;
                        case MetadataPropertyName:
                            metadata = reader.ReadString();
                            break;
                        case ReleaseLabelsPropertyName:
                            releaseLabels = options.Resolver.GetFormatter<IEnumerable<string>>().Deserialize(ref reader, options);
                            break;
                        case OriginalVersionPropertyName:
                            originalVersion = reader.ReadString();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new NuGetVersion(new Version(versionString), releaseLabels, metadata, originalVersion);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, NuGetVersion? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(4);
            writer.Write(VersionPropertyName);
            writer.Write(value.Version.ToString());
            writer.Write(MetadataPropertyName);
            writer.Write(value.Metadata);
            writer.Write(OriginalVersionPropertyName);
            writer.Write(value.OriginalVersion);
            writer.Write(ReleaseLabelsPropertyName);
            options.Resolver.GetFormatter<IEnumerable<string>>().Serialize(ref writer, value.ReleaseLabels, options);
        }
    }
}
