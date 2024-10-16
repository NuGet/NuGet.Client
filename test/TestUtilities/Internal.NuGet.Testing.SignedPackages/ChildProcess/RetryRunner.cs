// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CS1591

using System;
using Xunit.Abstractions;

namespace NuGet.Test.Utility
{
    public class RetryRunner
    {
        public static T RunWithRetries<T, E>(Func<T> func, int maxRetries = 1, ITestOutputHelper logger = null) where E : Exception
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
                        logger?.WriteLine($"Encountered exception during run attempt #{retryCount}: {exception.Message}");
                        logger?.WriteLine($"Retrying {retryCount} of {maxRetries}");
                    }
                }
            }
        }
    }
}
