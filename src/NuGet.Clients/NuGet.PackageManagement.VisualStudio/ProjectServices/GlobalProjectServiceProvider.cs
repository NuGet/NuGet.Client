// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Provides access to MEF components via global services catalog
    /// </summary>
    internal class GlobalProjectServiceProvider
    {
        private readonly IComponentModel _componentModel;

        public GlobalProjectServiceProvider(IComponentModel componentModel)
        {
            Assumes.Present(componentModel);

            _componentModel = componentModel;
        }

        public T GetGlobalService<T>() where T : class => _componentModel.GetService<T>();
    }
}
