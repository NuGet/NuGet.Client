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
        private const string FrameworkIdentifierPropertyName = "frameworkidentifier";
        private const string FrameworkVersionPropertyName = "frameworkversion";
        private const string FrameworkProfilePropertyName = "frameworkprofile";
        private const string PlatformIdentifierPropertyName = "platformidentifier";
        private const string PlatformVersionPropertyName = "platformversion";

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
                string? frameworkIdentifier = null;
                string? frameworkProfile = null;
                string? frameworkVersion = null;
                string? platformIdentifier = null;
                string? platformVersion = null;

                int propertyCount = reader.ReadMapHeader();

                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case FrameworkIdentifierPropertyName:
                            frameworkIdentifier = reader.ReadString();
                            break;

                        case FrameworkProfilePropertyName:
                            frameworkProfile = reader.ReadString();
                            break;

                        case FrameworkVersionPropertyName:
                            frameworkVersion = reader.ReadString();
                            break;

                        case PlatformIdentifierPropertyName:
                            platformIdentifier = reader.ReadString();
                            break;

                        case PlatformVersionPropertyName:
                            platformVersion = reader.ReadString();
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                return NuGetFramework.ParseComponents(
                    frameworkIdentifier,
                    frameworkVersion,
                    frameworkProfile,
                    platformIdentifier,
                    platformVersion);
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

            writer.WriteMapHeader(count: 5);
            writer.Write(FrameworkIdentifierPropertyName);
            writer.Write(value.Framework);
            writer.Write(FrameworkProfilePropertyName);
            writer.Write(value.Profile);
            writer.Write(FrameworkVersionPropertyName);
            writer.Write(value.Version.ToString());
            writer.Write(PlatformIdentifierPropertyName);
            writer.Write(value.Platform);
            writer.Write(PlatformVersionPropertyName);
            writer.Write(value.PlatformVersion.ToString());
        }
    }
}
