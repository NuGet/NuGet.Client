// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class ServiceContainer
    {
        private readonly ConcurrentDictionary<Type, object> _instances;

        internal ServiceContainer()
        {
            _instances = new ConcurrentDictionary<Type, object>();
        }

        internal void RegisterInstance<T>(T instance)
        {
            Assert.IsNotNull(instance, nameof(instance));

            _instances.AddOrUpdate(typeof(T), instance, (type, oldInstance) => instance);
        }

        internal T GetInstance<T>()
        {
            object instance;

            if (_instances.TryGetValue(typeof(T), out instance))
            {
                return (T)instance;
            }

            throw new ArgumentException($"No instance of type {typeof(T)} has been registered.");
        }
    }
}