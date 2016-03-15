using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class DownloadUtilityTests
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        [Fact]
        public void DownloadUtility_ReadsEnvironmentVariable()
        {
            VerifyEnvironmentVariable("42", TimeSpan.FromMilliseconds(42));
        }

        [Fact]
        public void DownloadUtility_DefaultTimeoutWhenInvalid()
        {
            VerifyEnvironmentVariable("10.99", DefaultTimeout);
        }

        [Fact]
        public void DownloadUtility_DefaultTimeoutWhenEmpty()
        {
            VerifyEnvironmentVariable("", DefaultTimeout);
        }

        [Fact]
        public void DownloadUtility_NegativeDisablesTimeout()
        {
            VerifyEnvironmentVariable("-42", Timeout.InfiniteTimeSpan);
        }

        [Fact]
        public void DownloadUtility_ZeroDisablesTimeout()
        {
            VerifyEnvironmentVariable("0", Timeout.InfiniteTimeSpan);
        }

        [Fact]
        public async Task DownloadUtility_TimesOutDownload()
        {
            // Arrange
            var target = new DownloadUtility { DownloadTimeout = TimeSpan.FromMilliseconds(500) };
            var content = "test content";
            var source = new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                DelayPerByte = TimeSpan.FromMilliseconds(100)
            };
            var destination = new MemoryStream();

            // Act & Assert
            var actual = await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await target.DownloadAsync(source, destination, "test", CancellationToken.None);
            });

            Assert.Equal("The download of 'test' took more than 500ms and therefore timed out.", actual.Message);
        }

        [Fact]
        public async Task DownloadUtility_TimesOutBufferAndProcess()
        {
            // Arrange
            var target = new DownloadUtility { DownloadTimeout = TimeSpan.FromMilliseconds(500) };
            var content = "test content";
            var source = new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                DelayPerByte = TimeSpan.FromMilliseconds(100)
            };
            var processed = false;

            // Act & Assert
            var actual = await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await target.BufferAndProcessAsync(
                    source,
                    stream =>
                    {
                        processed = true;
                        return Task.FromResult(0);
                    },
                    "test",
                    CancellationToken.None);
            });

            Assert.False(processed, "The content should not have been processed because the buffering should have timed out.");
            Assert.Equal("The download of 'test' took more than 500ms and therefore timed out.", actual.Message);
        }

        [Fact]
        public async Task DownloadUtility_AllowsNormalDownload()
        {
            // Arrange
            var target = new DownloadUtility();
            var expected = "test content";
            var source = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            var destination = new MemoryStream();

            // Act
            await target.DownloadAsync(source, destination, "test", CancellationToken.None);

            // Assert
            var actual = Encoding.UTF8.GetString(destination.ToArray());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task DownloadUtility_AllowsNormalBufferAndProcess()
        {
            // Arrange
            var target = new DownloadUtility();
            var expected = "test content";
            var source = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            // Act
            var output = await target.BufferAndProcessAsync(
                source,
                stream => new StreamReader(stream, Encoding.UTF8).ReadToEndAsync(),
                "test",
                CancellationToken.None);

            // Assert
            Assert.Equal(expected, output);
        }

        private static void VerifyEnvironmentVariable(string value, TimeSpan expected)
        {
            // Arrange
            var mock = new Mock<IEnvironmentVariableReader>();
            mock.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns(value);

            var target = new DownloadUtility { EnvironmentVariableReader = mock.Object };

            // Act
            var actual = target.DownloadTimeout;

            // Assert
            Assert.Equal(expected, actual);
            mock.Verify(x => x.GetEnvironmentVariable("nuget_download_timeout"), Times.Exactly(1));
        }

        private class SlowStream : Stream
        {
            private readonly Stream _innerStream;

            public SlowStream(Stream innerStream)
            {
                _innerStream = innerStream;
            }

            public TimeSpan DelayPerByte { get; set; }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _innerStream.Read(buffer, offset, count);
                Thread.Sleep(new TimeSpan(DelayPerByte.Ticks * read));
                return read;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotSupportedException();
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}
