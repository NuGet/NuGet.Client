// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Test.Server
{
    /// <summary>
    /// This class allocates ports while ensuring that:
    /// 1. Ports that are permanently taken (or taken for the duration of the test) are not being attempted to be used.
    /// 2. Ports are not shared across different tests (but you can allocate two different ports in the same test).
    /// 
    /// Gotcha: If another application grabs a port during the test, we have a race condition.
    /// </summary>
    public class PortReserver
    {
        private readonly int _basePort;

        public PortReserver(int basePort = 50231)
        {
            if (basePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basePort), "The base port must be greater than zero.");
            }
            
            _basePort = basePort;
        }

        public async Task<T> ExecuteAsync<T>(
            Func<int, CancellationToken, Task<T>> action,
            CancellationToken token)
        {
            int port = _basePort - 1;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                port++;

                if (port > 65535)
                {
                    throw new InvalidOperationException("Exceeded port range");
                }

                // ListUsedTCPPort prevents port contention with other apps.
                if (ListUsedTcpPort().Any(endPoint => endPoint.Port == port))
                {
                    continue;
                }

                // WaitForLockAsync prevents port contention with this app.
                string portLockName = $"NuGet-Port-{port}";
                var tryOnceCts = new CancellationTokenSource(TimeSpan.Zero);
                try
                {
                    var attemptedPort = port;
                    return await ConcurrencyUtilities.ExecuteWithFileLockedAsync<T>(
                        portLockName,
                        t => action(attemptedPort, token),
                        tryOnceCts.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private static IPEndPoint[] ListUsedTcpPort()
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

            return ipGlobalProperties.GetActiveTcpListeners();
        }
    }
}
