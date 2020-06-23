// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using StreamJsonRpc;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class NuGetServiceMessagePackRpcDescriptor : ServiceJsonRpcDescriptor
    {
        private static readonly MessagePackSerializerOptions MessagePackSerializerOptions;

        static NuGetServiceMessagePackRpcDescriptor()
        {
            var formatters = new IMessagePackFormatter[] { PackageSourceFormatter.Instance };
            var resolvers = new IFormatterResolver[] { MessagePackSerializerOptions.Standard.Resolver };

            MessagePackSerializerOptions = MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData).WithResolver(CompositeResolver.Create(formatters, resolvers));
        }

        internal NuGetServiceMessagePackRpcDescriptor(ServiceMoniker serviceMoniker)
            : base(serviceMoniker, Formatters.MessagePack, MessageDelimiters.BigEndianInt32LengthHeader)
        {
        }

        internal NuGetServiceMessagePackRpcDescriptor(ServiceMoniker serviceMoniker, Type clientInterface)
            : base(serviceMoniker, clientInterface, Formatters.MessagePack, MessageDelimiters.BigEndianInt32LengthHeader)
        {
        }

        protected NuGetServiceMessagePackRpcDescriptor(NuGetServiceMessagePackRpcDescriptor copyFrom)
            : base(copyFrom)
        {
        }

        protected override ServiceRpcDescriptor Clone() => new NuGetServiceMessagePackRpcDescriptor(this);

        protected override IJsonRpcMessageFormatter CreateFormatter()
        {
            Assumes.True(Formatter == Formatters.MessagePack);

            MessagePackFormatter formatter = base.CreateFormatter() as MessagePackFormatter ?? new MessagePackFormatter();
            formatter.SetMessagePackSerializerOptions(MessagePackSerializerOptions);
            return formatter;
        }
    }
}
