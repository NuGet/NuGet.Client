using System.Collections.Generic;

namespace NuGetConsole.Implementation.Console
{
    /// <summary>
    /// Simple console input history manager.
    /// </summary>
    internal class InputHistory
    {
        private const int MAX_HISTORY = 50;

        private readonly Queue<string> _inputs = new Queue<string>();

        public void Add(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                _inputs.Enqueue(input);
                if (_inputs.Count >= MAX_HISTORY)
                {
                    _inputs.Dequeue();
                }
            }
        }

        public IList<string> History
        {
            get { return _inputs.ToArray(); }
        }
    }
}
