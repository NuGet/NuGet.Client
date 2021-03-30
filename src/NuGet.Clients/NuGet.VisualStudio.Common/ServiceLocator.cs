// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;
using VsServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// This class unifies all the different ways of getting services within visual studio.
    /// </summary>
    // REVIEW: Make this internal 
    public static class ServiceLocator
    {
        public static void InitializePackageServiceProvider(IAsyncServiceProvider provider)
        {
            PackageServiceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static IAsyncServiceProvider PackageServiceProvider { get; private set; }

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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(GetInstanceAsync<TService>);
        }

        public static async Task<TService> GetInstanceAsync<TService>() where TService : class
        {
            // VS Threading Rule #1
            // Access to ServiceProvider and a lot of casts are performed in this method,
            // and so this method can RPC into main thread. Switch to main thread explictly, since method has STA requirement
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
        }

        public static TInterface GetGlobalService<TService, TInterface>() where TInterface : class
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(GetGlobalServiceAsync<TService, TInterface>);
        }

        public static async Task<TInterface> GetGlobalServiceAsync<TService, TInterface>() where TInterface : class
        {
            // VS Threading Rule #1
            // Access to ServiceProvider and a lot of casts are performed in this method,
            // and so this method can RPC into main thread. Switch to main thread explictly, since method has STA requirement
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (PackageServiceProvider != null)
            {
                var result = await PackageServiceProvider.GetServiceAsync(typeof(TService));
                var service = result as TInterface;
                if (service != null)
                {
                    return service;
                }
            }

            return Package.GetGlobalService(typeof(TService)) as TInterface;
        }

        public static async Task<TInterface> GetGlobalServiceFreeThreadedAsync<TService, TInterface>() where TInterface : class
        {
            if (PackageServiceProvider != null)
            {
                var result = await PackageServiceProvider.GetServiceAsync(typeof(TService));
                var service = result as TInterface;

                if (service != null)
                {
                    return service;
                }
            }

            return Package.GetGlobalService(typeof(TService)) as TInterface;
        }

        private static async Task<TService> GetDTEServiceAsync<TService>() where TService : class
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return dte != null ? QueryService(dte, typeof(TService)) as TService : null;
        }

        private static async Task<TService> GetComponentModelServiceAsync<TService>() where TService : class
        {
            IComponentModel componentModel = await GetGlobalServiceFreeThreadedAsync<SComponentModel, IComponentModel>();
            return componentModel?.GetService<TService>();
        }

        private static async Task<IServiceProvider> GetServiceProviderAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return GetServiceProviderFromDTE(dte);
        }

        private static object QueryService(DTE dte, Type serviceType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Guid guidService = serviceType.GUID;
            Guid riid = guidService;
            var serviceProvider = dte as VsServiceProvider;

            IntPtr servicePtr;
            int hr = serviceProvider.QueryService(ref guidService, ref riid, out servicePtr);

            if (hr != VSConstants.S_OK)
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
        private static IServiceProvider GetServiceProviderFromDTE(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IServiceProvider serviceProvider = new ServiceProvider(dte as VsServiceProvider);
            Debug.Assert(serviceProvider != null, "Service provider is null");
            return serviceProvider;
        }
    }
}
