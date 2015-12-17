using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace SynchronizationTestApp
{
    public class Program
    {
        private static bool _reportStarted = false;
        private static bool _shouldThrow = false;
        private static int _port = 0;

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new InvalidOperationException("usage: [--debug] filename port [-throw]");
            }

            if (args[0].Equals("--Debug"))
            {
                args = args.Skip(1).ToArray();

                Debugger.Launch();
                Debugger.Break();
            }

            var filename = args[0];

            if (string.IsNullOrEmpty(filename))
            {
                throw new InvalidOperationException("Pass a filename");
            }

            _port = int.Parse(args[1]);

            _shouldThrow = args.Length > 2 && args[2].Equals("-throw", StringComparison.Ordinal);

            _reportStarted = !_shouldThrow;

            _client = new TcpClient();

            var lockedTask = ConcurrencyUtilities.ExecuteWithFileLocked(filename, WaitInALock, CancellationToken.None);

            try
            {
                lockedTask.Wait();
            }
            catch (AggregateException)
            {
                return 2;
            }

            Console.WriteLine(filename);

            return 0;
        }

        static TcpClient _client;

        public static Task<object> WaitInALock(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                await _client.ConnectAsync("127.0.0.1", _port);

                var stream = _client.GetStream();
                var writer = new StreamWriter(stream);
                var reader = new StreamReader(stream);

                await writer.WriteLineAsync("Locked");
                await writer.FlushAsync();

                await reader.ReadLineAsync();

                if (_shouldThrow)
                {
                    // Do a SO instead
                    throw new InvalidOperationException("Aborted");
                }

                return new object();
            });
        }
    }
}