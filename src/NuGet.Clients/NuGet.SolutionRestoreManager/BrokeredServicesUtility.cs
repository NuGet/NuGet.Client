// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace NuGet.SolutionRestoreManager
{
    internal static class BrokeredServicesUtility
    {
        // It is very important that these names and versions are kept the same as the ones in NuGet.VisualStudio.Internal.Contracts.NuGetServices.
        internal const string NuGetSolutionServiceName = "NuGetSolutionService";
        internal const string NuGetSolutionServiceVersion = "1.0.0";

        internal static readonly ServiceRpcDescriptor NuGetSolutionService = new ServiceJsonRpcDescriptor(
            new ServiceMoniker(NuGetSolutionServiceName, new Version(NuGetSolutionServiceVersion)),
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        internal static BrokeredServiceFactory GetNuGetSolutionServicesFactory()
        {
            return (mk, options, sb, ct) => new ValueTask<object>(new NuGetSolutionService());
        }
    }
}
