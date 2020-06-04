// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class PackageSourceConverter : JsonConverter<PackageSource>
    {
        private const string NameProperty = "Name";
        private const string SourceProperty = "Source";
        private const string IsEnabledProperty = "IsEnabled";
        private const string IsMachineWideProperty = "IsMachineWide";

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override PackageSource ReadJson(JsonReader reader, Type objectType, PackageSource existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Null)
            {
                return new PackageSource("Undefined");
            }

            string name = token[NameProperty].Value<string>();
            string source = token[SourceProperty].Value<string>();
            bool isEnabled = token[IsEnabledProperty].Value<bool>();
            bool isMachineWide = token[IsMachineWideProperty].Value<bool>();

            return new PackageSource(source, name, isEnabled)
            {
                IsMachineWide = isMachineWide
            };
        }

        public override void WriteJson(JsonWriter writer, PackageSource value, JsonSerializer serializer)
        {
            // We will only serialize what the UI needs
            // TODO: Currently this can cause issues with the SaveAllPackageSources if something like Credentials is defined as it will be removed
            writer.WriteStartObject();
            writer.WritePropertyName(NameProperty);
            writer.WriteValue(value.Name);
            writer.WritePropertyName(SourceProperty);
            writer.WriteValue(value.Source);
            writer.WritePropertyName(IsEnabledProperty);
            writer.WriteValue(value.IsEnabled);
            writer.WritePropertyName(IsMachineWideProperty);
            writer.WriteValue(value.IsMachineWide);
            writer.WriteEndObject();
        }
    }
}
