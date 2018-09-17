// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Licenses
{
    internal class LicenseExpressionTokenizer
    {
        private static Tuple<char, string> OpeningBracket = new Tuple<char, string>('(', "(");
        private static Tuple<char, string> ClosingBracket = new Tuple<char, string>(')', ")");

        private readonly string _value;
        /// <summary>
        /// A tokenizer for a license expression.
        /// This implementation assumes that the input has been sanitized and that there are no invalid characters.
        /// </summary>
        /// <param name="value"></param>
        internal LicenseExpressionTokenizer(string value)
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
        internal bool HasValidCharacters()
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

        /// <summary>
        /// Given a string, tokenizes by space into operators and values. The considered operators are, AND, OR, WITH, (, and ). 
        /// </summary>
        /// <returns>Tokens</returns>
        internal IEnumerable<LicenseExpressionToken> Tokenize()
        {
            var potentialTokens = _value.Split(' ');
            foreach (var token in potentialTokens)
            {
                var processingToken = token;

                IList<LicenseExpressionToken> tokensAfterValue = null;

                while (processingToken.Length > 0 && (processingToken[0] == '(' || processingToken[0] == ')'))
                {
                    yield return ParseBracket(processingToken[0]);

                    processingToken = processingToken.Substring(1);
                }

                while (processingToken.Length > 0 && (processingToken[processingToken.Length - 1] == '(' || processingToken[processingToken.Length - 1] == ')'))
                {
                    if (tokensAfterValue == null)
                    {
                        tokensAfterValue = new List<LicenseExpressionToken>();

                    }
                    tokensAfterValue.Add(ParseBracket(processingToken[processingToken.Length - 1]));
                    processingToken = processingToken.Substring(0, processingToken.Length - 1);
                }

                if (!string.IsNullOrEmpty(processingToken))
                {
                    yield return ParseTokenType(processingToken);
                }

                if (tokensAfterValue != null)
                {
                    foreach (var tokenAfterValue in tokensAfterValue)
                    {
                        yield return tokenAfterValue;
                    }
                }

            }
        }

        private LicenseExpressionToken ParseBracket(char bracket)
        {
            if (bracket == '(')
            {
                return new LicenseExpressionToken(bracket.ToString(), LicenseTokenType.OPENING_BRACKET);
            }
            if (bracket == ')')
            {
                return new LicenseExpressionToken(bracket.ToString(), LicenseTokenType.CLOSING_BRACKET);
            }
            return null;
        }

        private LicenseExpressionToken ParseTokenType(string token)
        {
            var expressionToken = Enum.TryParse(value: token, result: out LicenseTokenType result);

            if (expressionToken && result.IsOperator())
            {
                return new LicenseExpressionToken(token, result);
            }
            else // We already covered the brackets earlier, so it has to be a value.
            {
                return new LicenseExpressionToken(token, LicenseTokenType.VALUE);

            }
        }
    }
}
