// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.RuntimeModel
{
    [JsonSourceGenerationOptions(
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(RuntimeGraphJsonModel))]
    internal sealed partial class JsonRuntimeFormatContext : JsonSerializerContext
    {
    }

    [JsonConverter(typeof(RuntimeGraphJsonModelConverter))]
    internal sealed class RuntimeGraphJsonModel
    {
        public List<RuntimeDescription>? Runtimes { get; set; }

        public List<CompatibilityProfile>? Supports { get; set; }
    }
}
