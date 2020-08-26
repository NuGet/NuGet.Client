// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;

namespace NuGet.Packaging.Test
{
    public static class StreamExtensions
    {
        public static string ReadToEnd(this Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }

        public static Stream AsStream(this string value)
        {
            return AsStream(value, Encoding.UTF8);
        }

        public static Stream AsStream(this string value, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(value));
        }
    }
}
