// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This class unifies all the different ways of getting services within visual studio.
    /// </summary>
    // REVIEW: Make this internal 
    public static class ServiceLocator
    {
        public static void InitializePackageServiceProvider(IServiceProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            PackageServiceProvider = provider;
        }

        public static IServiceProvider PackageServiceProvider { get; private set; }

        public static TService GetInstanceSafe<TService>() where TService : class
        {
            try
            {
                return GetInstance<TService>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static TService GetInstance<TService>() where TService : class
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    // VS Threading Rule #1
                    // Access to ServiceProvider and a lot of casts are performed in this method,
                    // and so this method can RPC into main thread. Switch to main thread explictly, since method has STA requirement
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Special case IServiceProvider
                    if (typeof(TService) == typeof(IServiceProvider))
                    {
                        var serviceProvider = await GetServiceProviderAsync();
                        return (TService)serviceProvider;
                    }

                    // then try to find the service as a component model, then try dte then lastly try global service
                    // Per bug #2072, avoid calling GetGlobalService() from within the Initialize() method of NuGetPackage class.
                    // Doing so is illegal and may cause VS to hang. As a result of that, we defer calling GetGlobalService to the last option.
                    var serviceFromDTE = await GetDTEServiceAsync<TService>();
                    if (serviceFromDTE != null)
                    {
                        return serviceFromDTE;
                    }

                    var serviceFromComponentModel = await GetComponentModelServiceAsync<TService>();
                    if (serviceFromComponentModel != null)
                    {
                        return serviceFromComponentModel;
                    }

                    var globalService = await GetGlobalServiceAsync<TService, TService>();
                    return globalService;
                });
        }

        public static TInterface GetGlobalService<TService, TInterface>() where TInterface : class
        {
            return ThreadHelper.JoinableTaskFactory.Run(GetGlobalServiceAsync<TService, TInterface>);
        }

        private static async Task<TInterface> GetGlobalServiceAsync<TService, TInterface>() where TInterface : class
        {
            // VS Threading Rule #1
            // Access to ServiceProvider and a lot of casts are performed in this method,
            // and so this method can RPC into main thread. Switch to main thread explictly, since method has STA requirement
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (PackageServiceProvider != null)
            {
                var result = PackageServiceProvider.GetService(typeof(TService));
                TInterface service = result as TInterface;
                if (service != null)
                {
                    return service;
                }
            }

            return Package.GetGlobalService(typeof(TService)) as TInterface;
        }

        private static async Task<TService> GetDTEServiceAsync<TService>() where TService : class
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return QueryService(dte, typeof(TService)) as TService;
        }

        private static async Task<TService> GetComponentModelServiceAsync<TService>() where TService : class
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            IComponentModel componentModel = await GetGlobalServiceAsync<SComponentModel, IComponentModel>();
            return componentModel.GetService<TService>();
        }

        private static async Task<IServiceProvider> GetServiceProviderAsync()
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return GetServiceProviderFromDTE(dte);
        }

        private static object QueryService(_DTE dte, Type serviceType)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            Guid guidService = serviceType.GUID;
            Guid riid = guidService;
            var serviceProvider = dte as VsServiceProvider;

            IntPtr servicePtr;
            int hr = serviceProvider.QueryService(ref guidService, ref riid, out servicePtr);

            if (hr != NuGetVSConstants.S_OK)
            {
                // We didn't find the service so return null
                return null;
            }

            object service = null;

            if (servicePtr != IntPtr.Zero)
            {
                service = Marshal.GetObjectForIUnknown(servicePtr);
                Marshal.Release(servicePtr);
            }

            return service;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "The caller is responsible for disposing this")]
        private static IServiceProvider GetServiceProviderFromDTE(_DTE dte)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            IServiceProvider serviceProvider = new ServiceProvider(dte as VsServiceProvider);
            Debug.Assert(serviceProvider != null, "Service provider is null");
            return serviceProvider;
        }
    }
}
