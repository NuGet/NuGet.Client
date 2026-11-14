// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    internal interface IJsonObjectWriter : IObjectWriter
    {
        void WriteObjectStart(JsonEncodedText name);

        void WriteNameValue(JsonEncodedText name, int value);

        void WriteNameValue(JsonEncodedText name, bool value);

        void WriteNameValue(JsonEncodedText name, string? value);

        void WriteNameArray(JsonEncodedText name, IEnumerable<string> values);

        void WriteNonEmptyNameArray(JsonEncodedText name, IEnumerable<string> values);

        void WriteArrayStart(JsonEncodedText name);
    }
}
