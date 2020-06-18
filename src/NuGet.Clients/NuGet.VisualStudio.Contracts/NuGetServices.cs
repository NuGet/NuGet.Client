// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ServiceHub.Framework;

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>
    /// 
    /// </summary>
    public static class NuGetServices
    {
        /// <summary>
        /// 
        /// </summary>
        public const string NuGetProjectServiceName = "Microsoft.VisualStudio.NuGet.NuGetProjectService";

        /// <summary>
        /// 
        /// </summary>
        public const string Version1 = "1.0";

        /// <summary>
        /// 
        /// </summary>
        public static ServiceRpcDescriptor NuGetProjectServiceV1 { get; } = new ServiceJsonRpcDescriptor(
            new ServiceMoniker(NuGetProjectServiceName, new System.Version(Version1)),
            ServiceJsonRpcDescriptor.Formatters.MessagePack,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
    }
}
