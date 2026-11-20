// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    internal readonly struct JsonObjectWriterAdapter : IJsonObjectWriter
    {
        private readonly IObjectWriter _writer;

        internal JsonObjectWriterAdapter(IObjectWriter writer)
        {
            _writer = writer;
        }

        public void WriteObjectStart() => _writer.WriteObjectStart();

        public void WriteObjectStart(string name) => _writer.WriteObjectStart(name);

        public void WriteObjectStart(JsonEncodedText name) => _writer.WriteObjectStart(name.Value);

        public void WriteObjectEnd() => _writer.WriteObjectEnd();

        public void WriteNameValue(string name, int value) => _writer.WriteNameValue(name, value);

        public void WriteNameValue(JsonEncodedText name, int value) => _writer.WriteNameValue(name.Value, value);

        public void WriteNameValue(string name, bool value) => _writer.WriteNameValue(name, value);

        public void WriteNameValue(JsonEncodedText name, bool value) => _writer.WriteNameValue(name.Value, value);

        public void WriteNameValue(string name, string? value) => _writer.WriteNameValue(name, value);

        public void WriteNameValue(JsonEncodedText name, string? value) => _writer.WriteNameValue(name.Value, value);

        public void WriteNameArray(string name, IEnumerable<string> values) => _writer.WriteNameArray(name, values);

        public void WriteNameArray(JsonEncodedText name, IEnumerable<string> values) =>
            _writer.WriteNameArray(name.Value, values);

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values) =>
            _writer.WriteNonEmptyNameArray(name, values);

        public void WriteNonEmptyNameArray(JsonEncodedText name, IEnumerable<string> values) =>
            _writer.WriteNonEmptyNameArray(name.Value, values);

        public void WriteArrayStart(string name) => _writer.WriteArrayStart(name);

        public void WriteArrayStart(JsonEncodedText name) => _writer.WriteArrayStart(name.Value);

        public void WriteArrayEnd() => _writer.WriteArrayEnd();
    }
}
