// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class SemaphoreSlimThrottle : IThrottle
    {
        private readonly SemaphoreSlim _semaphore;

        public SemaphoreSlimThrottle(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public async Task WaitAsync()
        {
            await _semaphore.WaitAsync();
        }

        public void Release()
        {
            _semaphore.Release();
        }

        public static SemaphoreSlimThrottle CreateBinarySemaphore()
        {
            return new SemaphoreSlimThrottle(new SemaphoreSlim(1));
        }
    }
}
