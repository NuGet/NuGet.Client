// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="List{T}"/> where the list is setup as an object
    /// </summary>
    internal class ListObjectConvertor<T> : JsonConverter<IList<T>>
    {
        public override IList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(IList<T>))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return new List<T>(0);

            }
            //We use JsonObjects for the arrays so we advance to the first property in the object which is the name/ver of the first library
            reader.Read();

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new List<T>(0);
            }

            var objectConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
            var listObjects = new List<T>();
            do
            {
                listObjects.Add(objectConverter.Read(ref reader, typeof(T), options));
                //At this point we're looking at the EndObject token for the object, need to advance.
                reader.Read();
            }
            while (reader.TokenType != JsonTokenType.EndObject);
            return listObjects;
        }

        public override void Write(Utf8JsonWriter writer, IList<T> value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue("hi");
            }
        }
    }
}
