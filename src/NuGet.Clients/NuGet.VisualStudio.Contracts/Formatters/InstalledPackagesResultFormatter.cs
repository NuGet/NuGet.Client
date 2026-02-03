// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.VisualStudio.Contracts
{
    internal sealed class InstalledPackagesResultFormatter : NuGetContractsMessagePackFormatter<InstalledPackagesResult>
    {
        private const string StatusPropertyName = "status";
        private const string PackagesPropertyName = "packages";

        internal static readonly IMessagePackFormatter<InstalledPackagesResult?> Instance = new InstalledPackagesResultFormatter();

        private InstalledPackagesResultFormatter()
        {
        }

        protected override InstalledPackagesResult? DeserializeCore(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            InstalledPackageResultStatus status = InstalledPackageResultStatus.Unknown;
            IReadOnlyCollection<NuGetInstalledPackage>? packages = null;

            int propertyCount = reader.ReadMapHeader();
            for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
            {
                switch (reader.ReadString())
                {
                    case StatusPropertyName:
                        status = options.Resolver.GetFormatter<InstalledPackageResultStatus>()!.Deserialize(ref reader, options);
                        break;
                    case PackagesPropertyName:
                        packages = options.Resolver.GetFormatter<IReadOnlyCollection<NuGetInstalledPackage>>()!.Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return NuGetContractsFactory.CreateInstalledPackagesResult(status, packages);
        }

        protected override void SerializeCore(ref MessagePackWriter writer, InstalledPackagesResult value, MessagePackSerializerOptions options)
        {
            writer.WriteMapHeader(count: 2);
            writer.Write(StatusPropertyName);
            options.Resolver.GetFormatter<InstalledPackageResultStatus>()!.Serialize(ref writer, value.Status, options);
            writer.Write(PackagesPropertyName);
            options.Resolver.GetFormatter<IReadOnlyCollection<NuGetInstalledPackage>>()!.Serialize(ref writer, value.Packages, options);
        }
    }
}
