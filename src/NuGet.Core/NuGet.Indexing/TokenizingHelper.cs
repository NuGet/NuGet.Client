// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NuGet.Indexing
{
    public static class TokenizingHelper
    {
        private static StringBuilder _stringBuilder;

        public static IEnumerable<string> CamelCaseSplit(string term)
        {
            if (term.Length == 0)
            {
                yield break;
            }

            if (term.Length == 1)
            {
                yield return term;
                yield break;
            }

            var word = Interlocked.Exchange(ref _stringBuilder, null);
            if (word == null)
            {
                word = new StringBuilder(term[0]);
            }
            else
            {
                word.Clear();
                word.Append(term[0]);
            }

            bool lastIsUpper = char.IsUpper(term[0]);
            bool lastIsLetter = char.IsLetter(term[0]);

            for (int i = 1; i < term.Length; i++)
            {
                bool currentIsUpper = char.IsUpper(term[i]);
                bool currentIsLetter = char.IsLetter(term[i]);

                if ((lastIsLetter && currentIsLetter) && (!lastIsUpper && currentIsUpper))
                {
                    yield return word.ToString();
                    word.Clear();
                }

                word.Append(term[i]);

                lastIsUpper = currentIsUpper;
                lastIsLetter = currentIsLetter;
            }

            yield return word.ToString();
        }

        private static ISet<string> _stopWords = new HashSet<string> 
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "i",
            "if", "in", "into", "is", "it", "its", "no", "not", "of", "on", "or", "s", "such",
            "that", "the", "their", "then", "there", "these", "they", "this", "to", 
            "was", "we", "will", "with"
        };

        public static ISet<string> GetStopWords()
        {
            return _stopWords;
        }
    }
}
