// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    internal abstract class StreamableJsonConverter<T> : JsonConverter<T>
    {
        public abstract T ReadWithStream(ref StreamingUtf8JsonReader reader, JsonSerializerOptions options);
    }
}
