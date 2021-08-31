// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NuGet.Packaging.Signing
{
    internal sealed class RetriableX509ChainBuildPolicy : IX509ChainBuildPolicy
    {
        // These properties are non-private only to facilitate testing.
        internal int RetryCount { get; }
        internal TimeSpan SleepInterval { get; }

        internal RetriableX509ChainBuildPolicy(int retryCount, TimeSpan sleepInterval)
        {
            if (retryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(retryCount));
            }

            if (sleepInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(sleepInterval));
            }

            RetryCount = retryCount;
            SleepInterval = sleepInterval;
        }

        public bool Build(X509Chain chain, X509Certificate2 certificate)
        {
            if (chain is null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            bool wasBuilt = chain.Build(certificate);

            for (var i = 0; i < RetryCount; ++i)
            {
                if (wasBuilt)
                {
                    break;
                }

                bool hasUntrustedRoot = false;

                foreach (X509ChainStatus chainStatus in chain.ChainStatus)
                {
                    if (chainStatus.Status.HasFlag(X509ChainStatusFlags.UntrustedRoot))
                    {
                        hasUntrustedRoot = true;
                        break;
                    }
                }

                if (!hasUntrustedRoot)
                {
                    break;
                }

                Thread.Sleep(SleepInterval);

                wasBuilt = chain.Build(certificate);
            }

            return wasBuilt;
        }
    }
}
