using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGetConsole;

namespace StandaloneConsole
{
    internal class PSExpansionSession : IEnumerable<string>
    {
        private readonly SimpleExpansion _expansion;
        private readonly string _initialText;

        public PSExpansionSession(SimpleExpansion expansion, string initialText)
        {
            _expansion = expansion;
            _initialText = initialText;
        }

        public IEnumerator<string> GetEnumerator()
        {
            switch (_expansion?.Expansions?.Count)
            {
                case 0:
                    return Enumerable.Empty<string>()
                        .GetEnumerator();
                case 1:
                    return new[] { Replace(_expansion.Expansions[0]) }
                        .AsEnumerable()
                        .GetEnumerator();
                default:
                    return RepeatForever();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private IEnumerator<string> RepeatForever()
        {
            while (true)
            {
                foreach (var newText in _expansion.Expansions.Select(Replace))
                {
                    yield return newText;
                }
            }
        }

        private string Replace(string item)
        {
            var builder = new StringBuilder(_initialText);
            builder.Remove(_expansion.Start, _expansion.Length);
            builder.Insert(_expansion.Start, item);
            return builder.ToString();
        }
    }
}
