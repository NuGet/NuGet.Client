// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Logging
{
    public class Logger
    {
        private static ILogger _logger = new NullLogger();

        public static ILogger Instance
        {
            get
            {
                return _logger;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _logger = value;
            }
        }
    }
}