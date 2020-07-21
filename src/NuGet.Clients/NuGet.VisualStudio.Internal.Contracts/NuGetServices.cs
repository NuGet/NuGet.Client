// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.ServiceHub.Framework;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public static class NuGetServices
    {
        private const string DeprecatedNuGetSolutionServiceName = "NuGetSolutionService";
        private const string DeprecatedNuGetSolutionServiceVersion = "1.0.0";
        private const string NuGetSolutionServiceName = "Microsoft.VisualStudio.NuGet.SolutionService";
        private const string NuGetSolutionServiceVersion = "1.0.0";
        private const string SourceProviderServiceName = "Microsoft.VisualStudio.NuGet.SourceProviderService";
        private const string SourceProviderServiceVersion = "1.0.0";
        private const string ProjectManagerProviderServiceName = "Microsoft.VisualStudio.NuGet.ProjectManagerService";
        private const string ProjectManagerServiceVersion = "1.0.0";

        /// <summary>
        /// A service descriptor for the NuGetSolutionService service. 
        /// </summary>
        public static readonly ServiceRpcDescriptor NuGetSolutionService = new ServiceJsonRpcDescriptor(
            new ServiceMoniker(NuGetSolutionServiceName, new Version(NuGetSolutionServiceVersion)),
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        public static readonly ServiceRpcDescriptor DeprecatedNuGetSolutionService = new ServiceJsonRpcDescriptor(
            new ServiceMoniker(DeprecatedNuGetSolutionServiceName, new Version(DeprecatedNuGetSolutionServiceVersion)),
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        public static readonly ServiceRpcDescriptor SourceProviderService = new NuGetServiceMessagePackRpcDescriptor(new ServiceMoniker(SourceProviderServiceName, new Version(SourceProviderServiceVersion)));
        public static readonly ServiceRpcDescriptor ProjectManagerService = new NuGetServiceMessagePackRpcDescriptor(new ServiceMoniker(ProjectManagerProviderServiceName, new Version(ProjectManagerServiceVersion)));
    }
}
