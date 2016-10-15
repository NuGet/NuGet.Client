// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Helper methods to acquire services via <see cref="IServiceProvider"/>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        public static EnvDTE.DTE GetDTE(this IServiceProvider serviceProvider)
        {
            return (EnvDTE.DTE)serviceProvider.GetService(typeof(EnvDTE.DTE));
        }

        public static TInterface GetService<TService, TInterface>(
            this IServiceProvider serviceProvider) 
            where TInterface : class
        {
            return serviceProvider.GetService(typeof(TService)) as TInterface;
        }
    }
}
