using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Sliding in time error threshold evaluator. 
    /// Verifies the number of fatal errors within last hour not bigger than reasonable threshold.
    /// Not thread-safe as all value converter calls happen in main UI thread.
    /// </summary>
    internal class ErrorFloodGate
    {
        // If we fail at least this high (failures/attempts), we'll shut off image loads.
        // TODO: Should we allow this to be overridden in nuget.config.
        private const double StopLoadingThreshold = 0.50;
        private const int SlidingExpirationInMinutes = 60;
        private const int MinFailuresCount = 5;
        private const int SecondsInOneTick = 5;
        private readonly DateTimeOffset _origin = DateTimeOffset.Now;
        private readonly Queue<int> _attempts = new Queue<int>();
        private readonly Queue<int> _failures = new Queue<int>();

        private DateTimeOffset _lastEvaluate = DateTimeOffset.Now;
        private bool _isOpen = false;

        public bool IsOpen
        {
            get
            {
                if (GetTicks(_lastEvaluate) > 1)
                {
                    var discardOlderThan1Hour = GetTicks(DateTimeOffset.Now.AddMinutes(-SlidingExpirationInMinutes));

                    ExpireOlderValues(_attempts, discardOlderThan1Hour);
                    ExpireOlderValues(_failures, discardOlderThan1Hour);

                    var attemptsCount = _attempts.Count;
                    var failuresCount = _failures.Count;
                    _isOpen = attemptsCount > 0 && failuresCount > MinFailuresCount && ((double)failuresCount / attemptsCount) > StopLoadingThreshold;
                    _lastEvaluate = DateTimeOffset.Now;
                }
                return _isOpen;
            }
        }

        private static void ExpireOlderValues(Queue<int> q, int expirationOffsetInTicks)
        {
            while (q.Count > 0 && q.Peek() < expirationOffsetInTicks)
            {
                q.Dequeue();
            }
        }

        public void ReportAttempt()
        {
            int ticks = GetTicks(_origin);
            _attempts.Enqueue(ticks);
        }

        public void ReportError()
        {
            int ticks = GetTicks(_origin);
            _failures.Enqueue(ticks);
        }

        // Ticks here are of 5sec long
        private static int GetTicks(DateTimeOffset origin)
        {
            return (int)((DateTimeOffset.Now - origin).TotalSeconds / SecondsInOneTick);
        }
    }
}
