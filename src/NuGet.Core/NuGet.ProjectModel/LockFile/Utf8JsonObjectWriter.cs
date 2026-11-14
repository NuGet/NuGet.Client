// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Adapts a <see cref="Utf8JsonWriter" /> to the neutral <see cref="IObjectWriter" /> contract.
    /// </summary>
    /// <remarks>
    /// This is a readonly value type so generic writer pipelines can preserve its concrete type. This allows the
    /// .NET Framework JIT to use value-type-specific code and constrained interface calls, avoiding boxing and
    /// repeated interface dispatch. Passing this writer through a non-generic <see cref="IObjectWriter" /> parameter
    /// would box it and lose that performance benefit.
    /// </remarks>
    internal readonly struct Utf8JsonObjectWriter : IJsonObjectWriter
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

        public void WriteObjectStart(JsonEncodedText name)
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

        public void WriteNameValue(JsonEncodedText name, int value)
        {
            _writer.WriteNumber(name, value);
        }

        public void WriteNameValue(string name, bool value)
        {
            _writer.WriteBoolean(name, value);
        }

        public void WriteNameValue(JsonEncodedText name, bool value)
        {
            _writer.WriteBoolean(name, value);
        }

        public void WriteNameValue(string name, string value)
        {
            _writer.WriteString(name, value);
        }

        public void WriteNameValue(JsonEncodedText name, string value)
        {
            _writer.WriteString(name, value);
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            _writer.WritePropertyName(name);
            WriteArrayValues(values);
        }

        public void WriteNameArray(JsonEncodedText name, IEnumerable<string> values)
        {
            _writer.WritePropertyName(name);
            WriteArrayValues(values);
        }

        private void WriteArrayValues(IEnumerable<string> values)
        {
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

        public void WriteNonEmptyNameArray(JsonEncodedText name, IEnumerable<string> values)
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

        public void WriteArrayStart(JsonEncodedText name)
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
