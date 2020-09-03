// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Frameworks;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class NuGetFrameworkFormatter : IMessagePackFormatter<NuGetFramework?>
    {
        private const string DotNetFrameworkNamePropertyName = "dotnetframeworkname";
        private const string DotNetPlatformNamePropertyName = "dotnetplatformname";

        internal static readonly IMessagePackFormatter<NuGetFramework?> Instance = new NuGetFrameworkFormatter();

        private NuGetFrameworkFormatter()
        {
        }

        public NuGetFramework? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? frameworkName = null;
                string? platformName = null;

                int propertyCount = reader.ReadMapHeader();

                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case DotNetFrameworkNamePropertyName:
                            frameworkName = reader.ReadString();
                            break;

                        case DotNetPlatformNamePropertyName:
                            platformName = reader.ReadString();
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                return NuGetFramework.ParseComponents(frameworkName, platformName);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, NuGetFramework? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(DotNetFrameworkNamePropertyName);
            writer.Write(value.DotNetFrameworkName);
            writer.Write(DotNetPlatformNamePropertyName);
            writer.Write(value.DotNetPlatformName);
        }
    }
}
