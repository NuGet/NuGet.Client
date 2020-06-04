// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ServiceHub.Framework;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class NuGetBrokeredServices
    {
        public const string SourceProviderServiceName = "NuGet.SourceProviderService";
        public const string SourceProviderServiceVersion = "1.0.0";

        public static readonly ServiceRpcDescriptor SourceProviderService = new ServiceJsonRpcDescriptorWithNuGetCoreConverters(
              new ServiceMoniker(SourceProviderServiceName, new Version(SourceProviderServiceVersion)),
              ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
    }
}
