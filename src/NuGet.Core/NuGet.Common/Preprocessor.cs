using System;
using System.IO;
using System.Text;

namespace NuGet.Common
{
    /// <summary>
    /// Simple token replacement system for content files.
    /// </summary>
    public class Preprocessor
    {
        public static string Process(Func<Stream> fileStreamFactory, Func<string, string> tokenReplacement)
        {
            using (var stream = fileStreamFactory())
            {
                return Process(stream, tokenReplacement);
            }
        }

        public static string Process(Stream stream, Func<string, string> tokenReplacement)
        {
            string text;
            using (var streamReader = new StreamReader(stream))
            {
                text = streamReader.ReadToEnd();
            }

            var tokenizer = new Tokenizer(text);
            StringBuilder result = new StringBuilder();
            for (; ; )
            {
                Token token = tokenizer.Read();
                if (token == null)
                {
                    break;
                }

                if (token.Category == TokenCategory.Variable)
                {
                    var replaced = tokenReplacement(token.Value);
                    result.Append(replaced);
                }
                else
                {
                    result.Append(token.Value);
                }
            }

            return result.ToString();
        }
    }
}
