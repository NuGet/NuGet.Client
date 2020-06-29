// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using MessagePack;
using MessagePack.Formatters;
using NuGet.Frameworks;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class NuGetFrameworkFormatter : IMessagePackFormatter<NuGetFramework?>
    {
        private const string FrameworkPropertyName = "framework";
        private const string FrameworkVersionPropertyName = "frameworkversion";
        private const string ProfilePropertyName = "profile";

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
                string? framework = null;
                string? profile = null;
                Version? version = null;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case FrameworkPropertyName:
                            framework = reader.ReadString();
                            break;
                        case FrameworkVersionPropertyName:
                            version = new Version(reader.ReadString());
                            break;
                        case ProfilePropertyName:
                            profile = reader.ReadString();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new NuGetFramework(framework, version, profile);
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

            writer.WriteMapHeader(3);
            writer.Write(FrameworkPropertyName);
            writer.Write(value.Framework);
            writer.Write(FrameworkVersionPropertyName);
            writer.Write(value.Version.ToString());
            writer.Write(ProfilePropertyName);
            writer.Write(value.Profile);
        }
    }
}
