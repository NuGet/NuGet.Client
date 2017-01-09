// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    public class NullLogger : ILogger
    {
        private static ILogger _instance;

        public static ILogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NullLogger();
                }

                return _instance;
            }
        }

        public void LogDebug(string data) { }

        public void LogError(string data) { }

        public void LogInformation(string data) { }

        public void LogMinimal(string data) { }

        public void LogVerbose(string data) { }

        public void LogWarning(string data) { }

        public void LogInformationSummary(string data) { }
        
        public void LogErrorSummary(string data) { }
    }
}
