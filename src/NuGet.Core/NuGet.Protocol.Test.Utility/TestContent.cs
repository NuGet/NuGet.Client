﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Test.Utility
{
    public class TestContent : HttpContent
    {
        private MemoryStream _stream;

        public TestContent(string s)
        {
            _stream = new MemoryStream(Encoding.UTF8.GetBytes(s));
            _stream.Seek(0, SeekOrigin.Begin);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return _stream.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _stream.Length;
            return true;
        }
    }
}
