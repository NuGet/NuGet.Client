// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;

namespace NuGet.VisualStudio.Contracts
{
    internal sealed class NuGetInstalledPackageFormatter : NuGetContractsMessagePackFormatter<NuGetInstalledPackage>
    {
        private const string IdPropertyName = "id";
        private const string RequestedRangePropertyName = "requestedRange";
        private const string VersionPropertyName = "version";
        private const string InstallPathPropertyName = "installPath";
        private const string DirectDependencyPropertyName = "directDependency";

        internal static readonly IMessagePackFormatter<NuGetInstalledPackage?> Instance = new NuGetInstalledPackageFormatter();

        private NuGetInstalledPackageFormatter()
        {
        }

        protected override NuGetInstalledPackage? DeserializeCore(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string? id = null;
            string? requestedRange = null;
            string? version = null;
            string? installPath = null;
            bool directDependency = false;

            int propertyCount = reader.ReadMapHeader();
            for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
            {
                switch (reader.ReadString())
                {
                    case IdPropertyName:
                        id = reader.ReadString();
                        break;
                    case RequestedRangePropertyName:
                        requestedRange = reader.ReadString();
                        break;
                    case VersionPropertyName:
                        version = reader.ReadString();
                        break;
                    case InstallPathPropertyName:
                        installPath = reader.ReadString();
                        break;
                    case DirectDependencyPropertyName:
                        directDependency = reader.ReadBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return NuGetContractsFactory.CreateNuGetInstalledPackage(id!, requestedRange, version, installPath, directDependency);
        }

        protected override void SerializeCore(ref MessagePackWriter writer, NuGetInstalledPackage value, MessagePackSerializerOptions options)
        {
            writer.WriteMapHeader(count: 5);
            writer.Write(IdPropertyName);
            writer.Write(value.Id);
            writer.Write(RequestedRangePropertyName);
            writer.Write(value.RequestedRange);
            writer.Write(VersionPropertyName);
            writer.Write(value.Version);
            writer.Write(InstallPathPropertyName);
            writer.Write(value.InstallPath);
            writer.Write(DirectDependencyPropertyName);
            writer.Write(value.DirectDependency);
        }
    }
}
