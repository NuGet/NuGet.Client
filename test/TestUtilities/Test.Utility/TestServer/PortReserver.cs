// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        private static ConcurrentDictionary<string, bool> PortLock = new ConcurrentDictionary<string, bool>();
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
                if (!IsTcpPortAvailable(port))
                {
                    continue;
                }

                // WaitForLockAsync prevents port contention with this app.
                string portLockName = $"NuGet-Port-{port}";

                try
                {
                    if (PortLock.TryAdd(portLockName, true))
                    {
                        // Run the action within the lock
                        return await action(port, token);
                    }
                }
                catch (OverflowException)
                {
                    throw;
                }
                finally
                {
                    PortLock.TryRemove(portLockName, out _);
                }
            }
        }

        private static bool IsTcpPortAvailable(int port)
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                tcpListener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}
