// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

#pragma warning disable CS1591

using System;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess
{
    public class RetryRunner
    {
        public static T RunWithRetries<T, E>(Func<T> func, int maxRetries = 1, Action<string> logLine = null) where E : Exception
        {
            {
                int retryCount = 0;

                while (true)
                {
                    try
                    {
                        return func();
                    }
                    catch (E exception)
                    {
                        if (retryCount >= maxRetries)
                        {
                            throw exception;
                        }

                        retryCount++;
                        logLine?.Invoke($"Encountered exception during run attempt #{retryCount}: {exception.Message}");
                        logLine?.Invoke($"Retrying {retryCount} of {maxRetries}");
                    }
                }
            }
        }
    }
}
