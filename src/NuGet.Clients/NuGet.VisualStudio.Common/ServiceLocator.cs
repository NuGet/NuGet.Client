// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using VsServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// This class unifies all the different ways of getting services within visual studio.
    /// </summary>
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

        /// <inheritdoc cref="GetInstanceAsync{TService}"/>
        public static TService GetInstance<TService>() where TService : class
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(GetInstanceAsync<TService>);
        }

        /// <summary>
        /// Fetches a service that may be registered with the DTE, MEF or the VS service provider.
        /// This method may switch to the UI thread.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns>The instance of the service request, <see langword="null"/> otherwise. </returns>
        /// <remarks>
        /// Prefer <see cref="GetComponentModelServiceAsync{TService}{TService}"/> over this requesting a MEF service.
        /// A general rule is that only non-NuGet VS services should be retrieved this method.
        /// </remarks>
        public static async Task<TService> GetInstanceAsync<TService>() where TService : class
        {
            // Try to find the service as a component model, then try dte then lastly try global service
            // Per bug #2072, avoid calling GetGlobalService() from within the Initialize() method of NuGetPackage class.
            // Doing so is illegal and may make VS to stop responding. As a result of that, we defer calling GetGlobalService to the last option.

            // Special case IServiceProvider
            if (typeof(TService) == typeof(IServiceProvider))
            {
                var serviceProvider = await GetServiceProviderAsync();
                return (TService)serviceProvider;
            }

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

            return await AsyncServiceProvider.GlobalProvider.GetServiceAsync<TService, TInterface>();
        }

        /// <summary>
        /// Fetches a MEF registered service if available.
        /// This method should be called from a background thread only. 
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns>The instance of the service request, <see langword="null"/> otherwise. </returns>
        /// <remarks>
        /// This method should only be preferred when using MEF imports is not easily achievable.
        /// Prefer this over <see cref="GetInstanceAsync{TService}"/> when the service requesting a MEF service.
        /// A general rule is that internal NuGet services should call this method over <see cref="GetInstanceAsync{TService}"/>.
        /// This method can be called from the UI thread, but that's unnecessary and a bad practice. Never do things that don't need the UI thread, on the UI thread.
        /// </remarks>
        public static async Task<TService> GetComponentModelServiceAsync<TService>() where TService : class
        {
            IComponentModel componentModel = await GetGlobalServiceFreeThreadedAsync<SComponentModel, IComponentModel>();
            return componentModel?.GetService<TService>();
        }

        /// <inheritdoc cref="GetComponentModelServiceAsync{TService}"/>
        public static TService GetComponentModelService<TService>() where TService : class
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(GetComponentModelServiceAsync<TService>);
        }

        private static async Task<TService> GetDTEServiceAsync<TService>() where TService : class
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return dte != null ? QueryService(dte, typeof(TService)) as TService : null;
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

        private static async Task<IServiceProvider> GetServiceProviderAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await GetGlobalServiceAsync<SDTE, DTE>();
            return GetServiceProviderFromDTE(dte);
        }

        private static IServiceProvider GetServiceProviderFromDTE(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IServiceProvider serviceProvider = new ServiceProvider(dte as VsServiceProvider);
            Debug.Assert(serviceProvider != null, "Service provider is null");
            return serviceProvider;
        }
    }
}
