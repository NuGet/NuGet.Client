// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class PackageSourceFormatter : IMessagePackFormatter<PackageSource?>
    {
        private const string NamePropertyName = "name";
        private const string SourcePropertyName = "source";
        private const string IsEnabledPropertyName = "isenabled";
        private const string IsMachineWidePropertyName = "ismachinewide";

        internal static readonly IMessagePackFormatter<PackageSource?> Instance = new PackageSourceFormatter();

        private PackageSourceFormatter()
        {
        }

        public PackageSource? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? name = null;
                string? source = null;
                bool isEnabled = false;
                bool isMachineWide = false;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case NamePropertyName:
                            name = reader.ReadString();
                            break;
                        case SourcePropertyName:
                            source = reader.ReadString();
                            break;
                        case IsEnabledPropertyName:
                            isEnabled = reader.ReadBoolean();
                            break;
                        case IsMachineWidePropertyName:
                            isMachineWide = reader.ReadBoolean();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new PackageSource(source, name, isEnabled)
                {
                    IsMachineWide = isMachineWide
                };
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageSource? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(4);
            writer.Write(NamePropertyName);
            writer.Write(value.Name);
            writer.Write(SourcePropertyName);
            writer.Write(value.Source);
            writer.Write(IsEnabledPropertyName);
            writer.Write(value.IsEnabled);
            writer.Write(IsMachineWidePropertyName);
            writer.Write(value.IsMachineWide);
        }
    }
}
