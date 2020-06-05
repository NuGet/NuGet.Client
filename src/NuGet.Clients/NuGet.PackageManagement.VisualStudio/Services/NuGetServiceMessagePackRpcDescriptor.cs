// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using StreamJsonRpc;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class NuGetServiceMessagePackRpcDescriptor : ServiceJsonRpcDescriptor
    {
        private static readonly IFormatterResolver Resolver;

        static NuGetServiceMessagePackRpcDescriptor()
        {
            Resolver = CompositeResolver.Create(new IFormatterResolver[] {
                        NuGetRpcFormatterResolver.Instance,
                        StandardResolver.Instance,
             });
        }

        internal NuGetServiceMessagePackRpcDescriptor(ServiceMoniker serviceMoniker, MessageDelimiters messageDelimiter)
            : base(serviceMoniker, Formatters.MessagePack, messageDelimiter)
        {
        }

        internal NuGetServiceMessagePackRpcDescriptor(ServiceMoniker serviceMoniker, Type clientInterface, MessageDelimiters messageDelimiter)
            : base(serviceMoniker, clientInterface, Formatters.MessagePack, messageDelimiter)
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
            var options = MessagePackSerializerOptions.Standard.WithResolver(Resolver).WithSecurity(MessagePackSecurity.UntrustedData);
            formatter.SetMessagePackSerializerOptions(options);

            return formatter;
        }
    }
}
