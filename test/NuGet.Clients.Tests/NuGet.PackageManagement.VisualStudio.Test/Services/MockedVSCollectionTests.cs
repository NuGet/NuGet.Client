// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(MockedVS.Collection)]
    public abstract class MockedVSCollectionTests : IAsyncServiceProvider
    {
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>();

        public MockedVSCollectionTests(GlobalServiceProvider globalServiceProvider)
        {
            globalServiceProvider.Reset();

            ServiceLocator.InitializePackageServiceProvider(this);
        }

        protected void AddService<T>(Task<object> obj)
        {
            _services.Add(typeof(T), obj);
        }

        public Task<object> GetServiceAsync(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out Task<object> task))
            {
                return task;
            }

            return Task.FromResult<object>(null);
        }
    }
}
