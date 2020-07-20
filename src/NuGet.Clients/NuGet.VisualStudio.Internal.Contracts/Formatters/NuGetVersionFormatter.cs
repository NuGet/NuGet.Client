// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class NuGetVersionFormatter : IMessagePackFormatter<NuGetVersion?>
    {
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

                string? originalVersion = null;

                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case OriginalVersionPropertyName:
                            originalVersion = reader.ReadString();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return NuGetVersion.Parse(originalVersion);
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

            writer.WriteMapHeader(1);
            writer.Write(OriginalVersionPropertyName);
            writer.Write(value.OriginalVersion);
        }
    }
}
