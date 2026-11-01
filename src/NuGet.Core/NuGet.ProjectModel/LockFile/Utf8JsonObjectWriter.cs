// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    internal sealed class Utf8JsonObjectWriter : IObjectWriter
    {
        private readonly Utf8JsonWriter _writer;

        internal Utf8JsonObjectWriter(Utf8JsonWriter writer)
        {
            _writer = writer;
        }

        public void WriteObjectStart()
        {
            _writer.WriteStartObject();
        }

        public void WriteObjectStart(string name)
        {
            _writer.WritePropertyName(name);
            _writer.WriteStartObject();
        }

        public void WriteObjectEnd()
        {
            _writer.WriteEndObject();
        }

        public void WriteNameValue(string name, int value)
        {
            _writer.WriteNumber(name, value);
        }

        public void WriteNameValue(string name, bool value)
        {
            _writer.WriteBoolean(name, value);
        }

        public void WriteNameValue(string name, string value)
        {
            _writer.WriteString(name, value);
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            _writer.WritePropertyName(name);
            _writer.WriteStartArray();

            foreach (string value in values)
            {
                _writer.WriteStringValue(value);
            }

            _writer.WriteEndArray();
        }

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values)
        {
            using var enumerator = values.NoAllocEnumerate().GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return;
            }

            _writer.WritePropertyName(name);
            _writer.WriteStartArray();
            _writer.WriteStringValue(enumerator.Current);

            while (enumerator.MoveNext())
            {
                _writer.WriteStringValue(enumerator.Current);
            }

            _writer.WriteEndArray();
        }

        public void WriteArrayStart(string name)
        {
            _writer.WritePropertyName(name);
            _writer.WriteStartArray();
        }

        public void WriteArrayEnd()
        {
            _writer.WriteEndArray();
        }
    }
}
