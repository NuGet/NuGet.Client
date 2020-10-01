// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using NuGet.Configuration;

namespace NuGet.VisualStudio.Internal.Contracts
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class PackageSourceUpdateOptionsFormatter : IMessagePackFormatter<PackageSourceUpdateOptions?>
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private const string UpdateCredentialsPropertyName = "updatecredentials";
        private const string UpdateEnabledPropertyName = "updateenabled";

#pragma warning disable CS0618 // Type or member is obsolete
        internal static readonly IMessagePackFormatter<PackageSourceUpdateOptions?> Instance = new PackageSourceUpdateOptionsFormatter();
#pragma warning restore CS0618 // Type or member is obsolete

        private PackageSourceUpdateOptionsFormatter()
        {
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public PackageSourceUpdateOptions? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
#pragma warning restore CS0618 // Type or member is obsolete
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

#pragma warning disable CS0618 // Type or member is obsolete
                return new PackageSourceUpdateOptions(updateCredentials, updateEnabled);
#pragma warning restore CS0618 // Type or member is obsolete
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public void Serialize(ref MessagePackWriter writer, PackageSourceUpdateOptions? value, MessagePackSerializerOptions options)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 2);
            writer.Write(UpdateCredentialsPropertyName);
            writer.Write(value.UpdateCredentials);
            writer.Write(UpdateEnabledPropertyName);
            writer.Write(value.UpdateEnabled);
        }
    }
}
