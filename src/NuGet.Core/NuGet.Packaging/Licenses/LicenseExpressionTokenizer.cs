// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NuGet.Packaging.Licenses
{
    public class LicenseExpressionTokenizer
    {
        private static Tuple<char, string> OpeningBracket = new Tuple<char, string>('(', "(");
        private static Tuple<char, string> ClosingBracket = new Tuple<char, string>(')', ")");

        private readonly string _value;
        /// <summary>
        /// A tokenizer for a license expression.
        /// This implementation assumes that the input has been sanitized and that there are no invalid characters.
        /// </summary>
        /// <param name="value"></param>
        public LicenseExpressionTokenizer(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }
            _value = value.Trim();
        }

        /// <summary>
        /// The valid characters for a license identifier are a-zA-Z0-9.-+
        /// The valid characters for a license expression are the above whitespace and ().
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool HasValidCharacters()
        {
            for (var i = 0; i < _value.Length; i++)
            {
                // If the character is not among these characters
                if (!((_value[i] >= 'a' && _value[i] <= 'z') ||
                    (_value[i] >= 'A' && _value[i] <= 'Z') ||
                    (_value[i] >= '0' && _value[i] <= '9') ||
                    _value[i] == ' ' ||
                    _value[i] == '.' ||
                    _value[i] == '-' ||
                    _value[i] == '+' ||
                    _value[i] == '(' ||
                    _value[i] == ')'
                    ))
                {
                    return false;
                }
            }
            return true;
        }

        public IEnumerable<LicenseExpressionToken> Tokenize()
        {
            var potentialTokens = _value.Split(' ');
            foreach (var token in potentialTokens)
            {
                var processingToken = token;

                var openingBracketCount = 0;
                var closingBracketCount = 0;

                while (processingToken.Length > 0 && processingToken[0] == OpeningBracket.Item1)
                {
                    processingToken = processingToken.Substring(1);
                    openingBracketCount++;
                }

                while (processingToken.Length > 0 && processingToken[processingToken.Length - 1] == ClosingBracket.Item1)
                {
                    processingToken = processingToken.Substring(0, processingToken.Length - 1);
                    closingBracketCount++;
                }

                while (openingBracketCount-- > 0)
                {
                    yield return new LicenseExpressionToken(OpeningBracket.Item2, LicenseTokenType.OPENING_BRACKET);
                }

                if (!string.IsNullOrEmpty(processingToken))
                {
                    yield return ParseTokenType(processingToken);
                }

                while (closingBracketCount-- > 0)
                {
                    yield return new LicenseExpressionToken(ClosingBracket.Item2, LicenseTokenType.CLOSING_BRACKET);
                }

            }
        }

        private LicenseExpressionToken ParseTokenType(string token)
        {
            var expressionToken = Enum.TryParse(value: token, result: out LicenseTokenType result);

            if (expressionToken && result.IsOperator())
            {
                return new LicenseExpressionToken(token, result);
            }
            else // We already covered the brackets earlier, so it has to be avalue.
            {
                return new LicenseExpressionToken(token, LicenseTokenType.VALUE);

            }
        }
    }
}
