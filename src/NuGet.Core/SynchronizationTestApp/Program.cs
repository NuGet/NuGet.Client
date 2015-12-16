using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace SynchronizationTestApp
{
    public class Program
    {
        private static bool _reportStarted = false;

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new InvalidOperationException("usage: [--debug] filename [-throw]");
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

            bool shouldThrow = args.Length > 1 && args[1].Equals("-throw", StringComparison.Ordinal);
            _reportStarted = !shouldThrow;

            var lockedTask = ConcurrencyUtilities.ExecuteWithFileLocked(filename, WaitAMinute, CancellationToken.None);

            if (shouldThrow)
            {
                // make sure locking happens before we throw;
                lockedTask.Wait(100);

                throw new InvalidOperationException("Aborted");
            }
            else
            {
                lockedTask.Wait();
            }

            Console.WriteLine(filename);
        }

        public static Task<object> WaitAMinute(CancellationToken token)
        {
            if (_reportStarted)
            {
                Console.WriteLine("Locked");
            }

            return Task.Run<object>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return null;
            });
        }
    }
}
