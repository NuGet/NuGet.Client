// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal class JsonUtility
    {
        internal static void WriteObject<TItem>(JsonWriter writer, IEnumerable<TItem> items, Action<JsonWriter, TItem> writeItem)
        {
            writer.WriteStartObject();

            foreach (var item in items)
            {
                writeItem(writer, item);
            }

            writer.WriteEndObject();
        }
    }
}
