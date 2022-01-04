// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal class ITransitivePackageReferenceContextInfoFormatter : NuGetMessagePackFormatter<ITransitivePackageReferenceContextInfo>
    {
        internal static readonly IMessagePackFormatter<ITransitivePackageReferenceContextInfo?> Instance = new ITransitivePackageReferenceContextInfoFormatter();

        protected override ITransitivePackageReferenceContextInfo? DeserializeCore(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        protected override void SerializeCore(ref MessagePackWriter writer, ITransitivePackageReferenceContextInfo value, MessagePackSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
