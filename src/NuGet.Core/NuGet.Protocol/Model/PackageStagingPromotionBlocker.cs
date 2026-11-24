// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol
{
    public sealed class PackageStagingPromotionBlocker
    {
        [JsonConstructor]
        internal PackageStagingPromotionBlocker(string code, string message)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message))
            {
                throw new JsonException();
            }

            Code = code;
            Message = message;
        }

        public string Code { get; }
        public string Message { get; }
    }
}
