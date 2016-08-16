// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public static class StreamExtensions
    {
        public static readonly int BufferSize = 8192;

        public static async Task CopyToAsync(this Stream stream, Stream destination, CancellationToken token)
        {
            await stream.CopyToAsync(destination, BufferSize, token);
        }
    }
}
