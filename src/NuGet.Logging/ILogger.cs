// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Logging
{
    /// <summary>
    /// A generic interface for logging.
    /// </summary>
    public interface ILogger
    {
        void LogDebug(string data);

        void LogVerbose(string data);

        void LogInformation(string data);

        void LogWarning(string data);

        void LogError(string data);
    }
}
