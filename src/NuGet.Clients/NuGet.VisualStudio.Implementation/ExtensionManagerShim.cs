// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    internal class ExtensionManagerShim
    {
        private static Type IVsExtensionManagerType;
        private static Type SVsExtensionManagerType;
        private static MethodInfo TryGetInstalledExtensionMethod;
        private static bool TypesInitialized;

        private readonly object _extensionManager;

        public ExtensionManagerShim(object extensionManager, Action<string> errorHandler)
        {
            InitializeTypes(errorHandler);
            _extensionManager = extensionManager ?? AsyncPackage.GetGlobalService(SVsExtensionManagerType);
        }

        private static void InitializeTypes(Action<string> errorHandler)
        {
            if (TypesInitialized)
            {
                return;
            }

            try
            {
                Assembly extensionManagerAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .First(a => a.FullName.StartsWith("Microsoft.VisualStudio.ExtensionManager,", StringComparison.InvariantCultureIgnoreCase));
                SVsExtensionManagerType =
                    extensionManagerAssembly.GetType("Microsoft.VisualStudio.ExtensionManager.SVsExtensionManager");
                IVsExtensionManagerType =
                    extensionManagerAssembly.GetType("Microsoft.VisualStudio.ExtensionManager.IVsExtensionManager");
                TryGetInstalledExtensionMethod = IVsExtensionManagerType.GetMethod("TryGetInstalledExtension",
                    new[] { typeof(string), typeof(IInstalledExtension).MakeByRefType() });
                if (TryGetInstalledExtensionMethod == null || SVsExtensionManagerType == null)
                {
                    throw new Exception();
                }

                TypesInitialized = true;
            }
            catch
            {
                // if any of the types or methods cannot be loaded throw an error. this indicates that some API in
                // Microsoft.VisualStudio.ExtensionManager got changed.
                errorHandler(VsResources.PreinstalledPackages_ExtensionManagerError);
            }
        }

        public bool TryGetExtensionInstallPath(string extensionId, out string installPath)
        {
            installPath = null;
            object[] parameters = { extensionId, null };
            bool result = (bool)TryGetInstalledExtensionMethod.Invoke(_extensionManager, parameters);
            if (!result)
            {
                return false;
            }
            var extension = parameters[1] as IInstalledExtension;
            installPath = extension?.InstallPath;
            return true;
        }
    }
}
