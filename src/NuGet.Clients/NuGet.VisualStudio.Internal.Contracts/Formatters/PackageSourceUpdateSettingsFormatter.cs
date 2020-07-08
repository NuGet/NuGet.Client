// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Configuration;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class PackageSourceUpdateSettingsFormatter : IMessagePackFormatter<PackageSourceUpdateSettings?>
    {
        private const string UpdateCredentialsPropertyName = "updatecredentials";
        private const string UpdateEnabledPropertyName = "updateenabled";

        internal static readonly IMessagePackFormatter<PackageSourceUpdateSettings?> Instance = new PackageSourceUpdateSettingsFormatter();

        private PackageSourceUpdateSettingsFormatter()
        {
        }

        public PackageSourceUpdateSettings? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                bool updateCredentials = true;
                bool updateEnabled = true;

                int propertyCount = reader.ReadMapHeader();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    switch (reader.ReadString())
                    {
                        case UpdateCredentialsPropertyName:
                            updateCredentials = reader.ReadBoolean();
                            break;
                        case UpdateEnabledPropertyName:
                            updateEnabled = reader.ReadBoolean();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new PackageSourceUpdateSettings(updateCredentials, updateEnabled);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PackageSourceUpdateSettings? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(2);
            writer.Write(UpdateCredentialsPropertyName);
            writer.Write(value.UpdateCredentials);
            writer.Write(UpdateEnabledPropertyName);
            writer.Write(value.UpdateEnabled);
        }
    }
}
