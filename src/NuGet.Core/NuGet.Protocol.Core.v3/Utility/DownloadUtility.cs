using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class DownloadUtility
    {
        private const string DownloadTimeoutKey = "nuget_download_timeout";

        private IEnvironmentVariableReader _environmentVariableReader;

        public IEnvironmentVariableReader EnvironmentVariableReader 
        {
            get
            {
                if (_environmentVariableReader == null)
                {
                    _environmentVariableReader = new EnvironmentVariableWrapper();
                }

                return _environmentVariableReader;
            }

            set { _environmentVariableReader = value; }
        }

        private TimeSpan? _downloadTimeout;

        public TimeSpan DownloadTimeout
        {
            get
            {
                if (!_downloadTimeout.HasValue)
                {
                    var unparsedTimeout = EnvironmentVariableReader.GetEnvironmentVariable(DownloadTimeoutKey);
                    int timeoutMilliseconds;
                    if (!int.TryParse(unparsedTimeout, out timeoutMilliseconds))
                    {
                        _downloadTimeout = TimeSpan.FromMinutes(5);
                    }
                    else if(timeoutMilliseconds <= 0)
                    {
                        _downloadTimeout = Timeout.InfiniteTimeSpan;
                    }
                    else
                    {
                        _downloadTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                    }
                }

                return _downloadTimeout.Value;
            }

            set { _downloadTimeout = value; }
        }

        public async Task DownloadAsync(Stream source, Stream destination, string downloadName, CancellationToken token)
        {
            var timeoutMessage = string.Format(
                CultureInfo.CurrentCulture,
                Strings.DownloadTimeout,
                downloadName,
                (int) DownloadTimeout.TotalMilliseconds);

            await TimeoutUtility.StartWithTimeout(
                timeoutToken => source.CopyToAsync(destination, bufferSize: 8192, cancellationToken: timeoutToken),
                DownloadTimeout,
                timeoutMessage,
                token);

            await destination.FlushAsync(token);
        }

        public async Task<T> BufferAndProcessAsync<T>(Stream source, Func<Stream, Task<T>> process, string downloadName, CancellationToken token)
        {
            using (var memoryStream = new MemoryStream())
            {
                await DownloadAsync(source, memoryStream, downloadName, token);

                memoryStream.Seek(0, SeekOrigin.Begin);

                return await process(memoryStream);
            }
        }
    }
}