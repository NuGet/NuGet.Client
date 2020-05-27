// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class NuGetBrokeredServices
    {
        public const string SourceRepositoryProviderServiceName = "NuGet.SourceRepositoryProviderService";
        public const string SourceRepositoryProviderServiceVersion = "1.0.0";


        public static readonly ServiceRpcDescriptor SourceRepositoryProviderService = new ServiceJsonRpcDescriptor(
              new ServiceMoniker(SourceRepositoryProviderServiceName, new Version(SourceRepositoryProviderServiceVersion)),
              ServiceJsonRpcDescriptor.Formatters.UTF8,
              ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        public static BrokeredServiceFactory GetSourceRepositoryProviderServiceFactory()
        {
            return (mk, options, sb, ct) => new ValueTask<object>(new NuGetSourceRepositoryService());
        }
    }
}
