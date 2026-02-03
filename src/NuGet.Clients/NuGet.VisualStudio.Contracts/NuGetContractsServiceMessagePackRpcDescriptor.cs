// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.ServiceHub.Framework;
using StreamJsonRpc;

namespace NuGet.VisualStudio.Contracts
{
    internal class NuGetContractsServiceMessagePackRpcDescriptor : ServiceJsonRpcDescriptor
    {
        private static readonly Lazy<MessagePackSerializerOptions> MessagePackSerializerOptions =
            new Lazy<MessagePackSerializerOptions>(CreateMessagePackSerializerOptions);

        internal NuGetContractsServiceMessagePackRpcDescriptor(ServiceMoniker serviceMoniker)
            : base(serviceMoniker, Formatters.MessagePack, MessageDelimiters.BigEndianInt32LengthHeader)
        {
        }

        protected NuGetContractsServiceMessagePackRpcDescriptor(NuGetContractsServiceMessagePackRpcDescriptor copyFrom)
            : base(copyFrom)
        {
        }

        protected override ServiceRpcDescriptor Clone() => new NuGetContractsServiceMessagePackRpcDescriptor(this);

        protected override IJsonRpcMessageFormatter CreateFormatter()
        {
            MessagePackFormatter formatter = base.CreateFormatter() as MessagePackFormatter ?? new MessagePackFormatter();
            MessagePackSerializerOptions options = MessagePackSerializerOptions.Value;

            formatter.SetMessagePackSerializerOptions(options);

            return formatter;
        }

        internal static IMessagePackFormatter[] CreateMessagePackFormatters()
        {
            return
            [
                InstalledPackagesResultFormatter.Instance,
                NuGetInstalledPackageFormatter.Instance
            ];
        }

        private static MessagePackSerializerOptions CreateMessagePackSerializerOptions()
        {
            IMessagePackFormatter[] formatters = CreateMessagePackFormatters();
            var resolvers = new IFormatterResolver[] { MessagePack.MessagePackSerializerOptions.Standard.Resolver };

            return MessagePack.MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithResolver(CompositeResolver.Create(formatters, resolvers));
        }
    }
}
