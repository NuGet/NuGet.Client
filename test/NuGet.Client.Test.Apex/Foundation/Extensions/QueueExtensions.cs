using System;
using System.Collections.Generic;

namespace NuGetClient.Test.Foundation.Extensions
{
    public static class QueueExtensions
    {
        /// <summary>
        /// Dequeue a count of items
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is less than 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the queue doesn't contain enough items. (The queue will be emptied)</exception>
        public static IEnumerable<T> Dequeue<T>(this Queue<T> queue, int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            List<T> dequeued = new List<T>();
            for (int i = 0; i < count; i++)
            {
                dequeued.Add(queue.Dequeue());
            }
            return dequeued;
        }
    }
}
