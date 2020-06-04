// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using StreamJsonRpc;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class ServiceJsonRpcDescriptorWithNuGetCoreConverters : ServiceJsonRpcDescriptor
    {
        internal ServiceJsonRpcDescriptorWithNuGetCoreConverters(ServiceMoniker serviceMoniker, MessageDelimiters messageDelimiter)
            : base(serviceMoniker, Formatters.MessagePack, messageDelimiter)
        {
        }

        internal ServiceJsonRpcDescriptorWithNuGetCoreConverters(ServiceMoniker serviceMoniker, Type clientInterface, MessageDelimiters messageDelimiter)
            : base(serviceMoniker, clientInterface, Formatters.MessagePack, messageDelimiter)
        {
        }

        protected ServiceJsonRpcDescriptorWithNuGetCoreConverters(ServiceJsonRpcDescriptorWithNuGetCoreConverters copyFrom)
            : base(copyFrom)
        {
        }

        protected override ServiceRpcDescriptor Clone() => new ServiceJsonRpcDescriptorWithNuGetCoreConverters(this);

        protected override IJsonRpcMessageFormatter CreateFormatter()
        {
            Assumes.True(Formatter == Formatters.MessagePack);

            JsonMessageFormatter formatter = base.CreateFormatter() as JsonMessageFormatter ?? new JsonMessageFormatter();

            formatter.JsonSerializer.Converters.Add(new PackageSourceConverter());

            return formatter;
        }
    }
}
