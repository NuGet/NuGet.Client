// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="PackageSpec"/>
    /// </summary>
    internal class PackageSpecConverter : JsonConverter<PackageSpec>
    {
        public override PackageSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonPackageSpecReader.GetPackageSpec(ref reader, options, name: string.Empty, packageSpecPath: string.Empty, snapshotValue: string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, PackageSpec value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
