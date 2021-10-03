// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Xunit;

namespace NuGet.Common.Test
{
    [CollectionDefinition(TestName, DisableParallelization = true)]
    public class LocalizedTestCollection
    {
        public const string TestName = "LocalizedTests";

        public static CultureInfo DefaultCurrentUICulture;

        public static CultureInfo DefaultCurrentCulture;

        private static readonly object LockObject = new();

        static LocalizedTestCollection()
        {
            lock (LockObject)
            {
                DefaultCurrentCulture = CultureInfo.CurrentCulture;
                DefaultCurrentUICulture = CultureInfo.CurrentUICulture;
            }
        }

        public static void Reset()
        {
            lock(LockObject)
            {
                CultureInfo.CurrentCulture = DefaultCurrentCulture;
                CultureInfo.CurrentUICulture = DefaultCurrentUICulture;
            }
        }
    }
}
