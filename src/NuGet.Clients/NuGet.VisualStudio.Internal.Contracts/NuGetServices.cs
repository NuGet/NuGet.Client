// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.ServiceHub.Framework;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public static class NuGetServices
    {
        private const string NuGetSolutionServiceName = "NuGetSolutionService";
        private const string NuGetSolutionServiceVersion = "1.0.0";
        private const string SourceProviderServiceName = "NuGet.SourceProviderService";
        private const string SourceProviderServiceVersion = "1.0.0";

        /// <summary>
        /// A service descriptor for the NuGetSolutionService service. 
        /// </summary>
        public static ServiceRpcDescriptor NuGetSolutionService = new ServiceJsonRpcDescriptor(
            new ServiceMoniker(NuGetSolutionServiceName, new Version(NuGetSolutionServiceVersion)),
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        public static readonly ServiceRpcDescriptor SourceProviderService = new NuGetServiceMessagePackRpcDescriptor(new ServiceMoniker(SourceProviderServiceName, new Version(SourceProviderServiceVersion)));
    }
}
