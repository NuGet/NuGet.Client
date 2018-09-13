using System;
using System.IO;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class NoSyncReadStream : DownloadTimeoutStream
    {
        public NoSyncReadStream(Stream stream)
            : base("nosync", stream, TimeSpan.FromMinutes(1))
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Assert.True(false, "READ should not be called");
            throw new InvalidOperationException("test failed!! Read should not be called!");
        }
    }
}
