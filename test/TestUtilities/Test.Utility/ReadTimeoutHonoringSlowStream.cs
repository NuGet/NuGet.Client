// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Utility
{
    public class ReadTimeoutHonoringSlowStream : SlowStream
    {
        private readonly Stream _innerStream;
        private readonly CancellationToken _cancellationToken;
<<<<<<< HEAD
=======
        private int _readTimeout = int.MaxValue;
>>>>>>> Fix threadpool load induced delayed responses to plugin requests

        public ReadTimeoutHonoringSlowStream(Stream innerStream)
            : this(innerStream, CancellationToken.None)
        {
        }

        public ReadTimeoutHonoringSlowStream(Stream innerStream, CancellationToken cancellationToken)
            : base(innerStream, cancellationToken)
        {
            _innerStream = innerStream;
            _cancellationToken = cancellationToken;
        } 

        public override int Read(byte[] buffer, int offset, int count)
        {
            OnRead?.Invoke(buffer, offset, count);
            var read = _innerStream.Read(buffer, offset, count);

            try
            {
                var expectedDelayInMs = DelayPerByte.TotalMilliseconds * read;
<<<<<<< HEAD
                if (ReadTimeout > expectedDelayInMs)
=======
                if (_readTimeout > expectedDelayInMs)
>>>>>>> Fix threadpool load induced delayed responses to plugin requests
                {
                    Task.Delay(new TimeSpan(DelayPerByte.Ticks * read)).Wait(_cancellationToken);
                }
                else
                {
<<<<<<< HEAD
                    Task.Delay(ReadTimeout).Wait(_cancellationToken);
                    throw new IOException($"..timed out because no data was received for {ReadTimeout}ms.", new TimeoutException());
=======
                    Task.Delay(_readTimeout).Wait(_cancellationToken);
                    throw new IOException($"..timed out because no data was received for {_readTimeout}ms.", new TimeoutException());
>>>>>>> Fix threadpool load induced delayed responses to plugin requests
                }
            }
            catch (OperationCanceledException)
            {
            }

            return read;
        }

<<<<<<< HEAD
        public override int ReadTimeout { get; set; } = Timeout.Infinite;
=======
        public override int ReadTimeout { get => _readTimeout; set => _readTimeout = value; }
>>>>>>> Fix threadpool load induced delayed responses to plugin requests
        public override bool CanTimeout => true;
    }
}
