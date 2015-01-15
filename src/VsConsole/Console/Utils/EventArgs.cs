using System;

namespace NuGetConsole
{
    class EventArgs<T> : EventArgs
    {
        public T Arg { get; private set; }

        public EventArgs(T arg)
        {
            this.Arg = arg;
        }
    }
}
