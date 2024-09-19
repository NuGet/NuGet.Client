// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    internal sealed class NuGetSettingsSerializer
    {
        private const int BufferSize = 4096;
        private readonly JsonSerializerOptions _options;

        internal NuGetSettingsSerializer()
        {
            _options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true,
                DefaultBufferSize = BufferSize,
            };
        }

        internal async Task<NuGetSettings> DeserializeAsync(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<NuGetSettings>(stream, _options);
        }

        internal async Task SerializeAsync(Stream stream, NuGetSettings settings)
        {
            await JsonSerializer.SerializeAsync(stream, settings, _options);
        }
    }
}
