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
        private static bool _abandonLock = false;
        private static int _port = 0;
        public static readonly string AbandonSwitch = "-abandon";
        public static readonly string DebugSwitch = "--debug";

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new InvalidOperationException($"usage: [{DebugSwitch}] filename port [{AbandonSwitch}]");
            }

            if (args[0].Equals(DebugSwitch, StringComparison.Ordinal))
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

            _abandonLock = args.Length > 2 && args[2].Equals(AbandonSwitch, StringComparison.Ordinal);

            _reportStarted = !_abandonLock;

            _client = new TcpClient();

            var lockedTask = ConcurrencyUtilities.ExecuteWithFileLockedAsync(filename, WaitInALock, CancellationToken.None);

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

                if (_abandonLock)
                {
                    // Kill the process so if the locking mechanism doesn't deal with abandoned locks
                    // it will become evident to the test consuming this app
                    Process.GetCurrentProcess().Kill();
                }

                return new object();
            });
        }
    }
}