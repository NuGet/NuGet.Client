using System.Text;

namespace NuGet.Test.Utility
{
    internal class LockedStringBuilder
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly object _lock = new object();

        public void AppendLine(string value)
        {
            lock (_lock)
            {
                _builder.AppendLine(value);
            }
        }

        public override string ToString()
        {
            lock (_lock)
            {
                return _builder.ToString();
            }
        }
    }
}
