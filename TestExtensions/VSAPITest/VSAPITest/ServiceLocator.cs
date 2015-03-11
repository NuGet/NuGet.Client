using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using VsServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace MicrosoftCorp.VSAPITest
{
    /// <summary>
    /// Copied from NuGet.Core
    /// </summary>
    public static class ServiceLocator
    {
        public static void InitializePackageServiceProvider(IServiceProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            PackageServiceProvider = provider;
        }

        public static IServiceProvider PackageServiceProvider
        {
            get;
            private set;
        }

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
            // Special case IServiceProvider
            if (typeof(TService) == typeof(IServiceProvider))
            {
                return (TService)GetServiceProvider();
            }

            // then try to find the service as a component model, then try dte then lastly try global service
            // Per bug #2072, avoid calling GetGlobalService() from within the Initialize() method of NuGetPackage class. 
            // Doing so is illegal and may cause VS to hang. As a result of that, we defer calling GetGlobalService to the last option.
            return GetDTEService<TService>() ??
                  GetComponentModelService<TService>() ??
                  GetGlobalService<TService, TService>();
        }

        public static TInterface GetGlobalService<TService, TInterface>() where TInterface : class
        {
            if (PackageServiceProvider != null)
            {
                TInterface service = PackageServiceProvider.GetService(typeof(TService)) as TInterface;
                if (service != null)
                {
                    return service;
                }
            }

            return (TInterface)Package.GetGlobalService(typeof(TService));
        }

        private static TService GetDTEService<TService>() where TService : class
        {
            var dte = GetGlobalService<SDTE, DTE>();
            return (TService)QueryService(dte, typeof(TService));
        }

        private static TService GetComponentModelService<TService>() where TService : class
        {
            IComponentModel componentModel = GetGlobalService<SComponentModel, IComponentModel>();
            return componentModel.GetService<TService>();
        }

        private static IServiceProvider GetServiceProvider()
        {
            var dte = GetGlobalService<SDTE, DTE>();
            return GetServiceProvider(dte);
        }

        /// <summary>
        /// Internal test hook for adding services
        /// </summary>
        internal static IDictionary<Type, object> TestServiceCache { private get; set; }

        private static object QueryService(_DTE dte, Type serviceType)
        {
            // internal test hook for unit tests
            if (TestServiceCache != null)
            {
                object testService = null;
                if (TestServiceCache.TryGetValue(serviceType, out testService))
                {
                    return testService;
                }
            }

            Guid guidService = serviceType.GUID;
            Guid riid = guidService;
            var serviceProvider = dte as VsServiceProvider;

            IntPtr servicePtr;
            int hr = serviceProvider.QueryService(ref guidService, ref riid, out servicePtr);

            if (hr != 0)
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
        private static IServiceProvider GetServiceProvider(_DTE dte)
        {
            IServiceProvider serviceProvider = new ServiceProvider(dte as VsServiceProvider);
            Debug.Assert(serviceProvider != null, "Service provider is null");
            return serviceProvider;
        }
    }
}
