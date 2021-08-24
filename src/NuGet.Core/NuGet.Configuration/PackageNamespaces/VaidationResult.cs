// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Configuration
{
    public class ValidationResult
    {
        public NuGetLogCode ErrorCode { get; internal set; }
        public string ErrorMessage { get; internal set; }
        public bool Success { get; internal set; }
    }
}
