// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Xunit;

namespace NuGet.Common.Test
{
    [CollectionDefinition(TestName, DisableParallelization = true)]
    internal static class LocalizedTestCollection
    {
        public const string TestName = "LocalizedTests";

        private static CultureInfo CurrentUICulture;
        private static CultureInfo CurrentCulture;
        private static CultureInfo DefaultCurrentCulture;
        private static CultureInfo DefaultCurrentUICulture;

        private static readonly object LockObject = new();

        static LocalizedTestCollection()
        {
            lock (LockObject)
            {
                CurrentCulture = CultureInfo.CurrentCulture;
                CurrentUICulture = CultureInfo.CurrentUICulture;
                DefaultCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
                DefaultCurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
            }
        }

        public static void EnsureInit()
        {
            lock (LockObject)
            {
                // Here we make sure we captured initial culture info
            }
        }

        public static void Reset()
        {
            lock (LockObject)
            {
                CultureInfo.CurrentCulture = CurrentCulture;
                CultureInfo.CurrentUICulture = CurrentUICulture;
                CultureInfo.DefaultThreadCurrentCulture = DefaultCurrentCulture;
                CultureInfo.DefaultThreadCurrentUICulture = DefaultCurrentUICulture;
            }
        }
    }
}
