// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.Packaging.Licenses
{
    public static class NuGetLicenseExpressionParser
    {
        /// <summary>
        /// Parses a License Expression if valid.
        /// The expression would be parsed correct, even if non-standard exceptions are encountered. The non-standard Licenses/Exceptions have metadata on them with which the caller can make decisions.
        /// Based on the Shunting Yard algorithm. <see href="https://en.wikipedia.org/wiki/Shunting-yard_algorithm"/>
        /// This method first creates an postfix expression by separating the operators and operands.
        /// Later the postfix expression is evaluated into an object model that represents the expression. Note that brackets are dropped in this conversion and this is not round-trippable.
        /// The token precedence helps make sure that the expression is a valid infix one. 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns>NuGetLicenseExpression</returns>
        /// <exception cref="ArgumentException">If the expression has invalid characters</exception>
        /// <exception cref="ArgumentException">If the expression is empty or null.</exception>
        /// <exception cref="ArgumentException">If the expression itself is invalid. Example: MIT OR OR Apache-2.0, or the MIT or Apache-2.0, because the expressions are case sensitive.</exception>
        /// <exception cref="ArgumentException">If the expression's brackets are mismatched.</exception>
        /// <exception cref="ArgumentException">If the licenseIdentifier is deprecated.</exception>
        /// <exception cref="ArgumentException">If the exception identifier is deprecated.</exception>
        public static NuGetLicenseExpression Parse(string expression)
        {
            var tokens = GetTokens(expression);
            var operatorStack = new Stack<LicenseExpressionToken>();
            var operandStack = new Stack<Tuple<bool, object>>();

            var lastTokenType = LicenseTokenType.VALUE;
            var firstPass = true;

            foreach (var token in tokens)
            {
                var currentTokenType = token.TokenType;
                switch (token.TokenType)
                {
                    case LicenseTokenType.VALUE:
                        if (!firstPass && !token.TokenType.IsValidPrecedingToken(lastTokenType))
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidToken, token.Value));
                        }
                        // Add it to the operandstack. Only add it to the expression when you meet an operator
                        operandStack.Push(new Tuple<bool, object>(true, token));
                        break;

                    case LicenseTokenType.OPENING_BRACKET:
                        if (!firstPass && !token.TokenType.IsValidPrecedingToken(lastTokenType))
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidToken, token.Value));
                        }
                        operatorStack.Push(token);
                        break;

                    case LicenseTokenType.CLOSING_BRACKET:
                        if (firstPass || !token.TokenType.IsValidPrecedingToken(lastTokenType))
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidToken, token.Value));
                        }

                        // pop until we hit the opening bracket
                        while (operatorStack.Count > 0 && operatorStack.Peek().TokenType != LicenseTokenType.OPENING_BRACKET)
                        {
                            ProcessOperators(operatorStack, operandStack);
                        }

                        if (operatorStack.Count > 0)
                        {
                            // pop the bracket
                            operatorStack.Pop();
                        }
                        else
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_MismatchedParentheses));
                        }
                        break;

                    case LicenseTokenType.WITH:
                    case LicenseTokenType.AND:
                    case LicenseTokenType.OR:
                        if (firstPass && !token.TokenType.IsValidPrecedingToken(lastTokenType))
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidToken, token.Value));
                        }
                        if (operatorStack.Count == 0 || // The operator stack is empty
                            operatorStack.Peek().TokenType == LicenseTokenType.OPENING_BRACKET || // The last token is an opening bracket (treat it the same as empty
                            token.TokenType < operatorStack.Peek().TokenType) // An operator that has higher priority than the operator on the stack
                        {
                            operatorStack.Push(token);
                        }
                        // An operator that has lower/same priority than the operator on the stack
                        else if (token.TokenType >= operatorStack.Peek().TokenType)
                        {
                            ProcessOperators(operatorStack, operandStack);
                            operatorStack.Push(token);
                        }
                        break;

                    default:
                        throw new ArgumentException("Should not happen. File a bug with repro steps on NuGet/Home if seen.");
                }
                lastTokenType = currentTokenType;
                firstPass = false;
            }

            while (operatorStack.Count > 0)
            {
                if (operatorStack.Peek().TokenType != LicenseTokenType.OPENING_BRACKET)
                {
                    ProcessOperators(operatorStack, operandStack);
                }
                else
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_MismatchedParentheses));
                }
            }

            // This handles the no operators scenario. This check could be simpler, but it's dangerous to assume all scenarios have been handled by the above logic.
            // As written and as tested, you would never have more than 1 operand on the stack

            if (operandStack.Count != 1)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExpression));
            }
            else
            {
                var value = operandStack.Pop();

                return value.Item1 ? NuGetLicense.Parse(((LicenseExpressionToken)value.Item2).Value) : (NuGetLicenseExpression)value.Item2;
            }
        }

        /// <summary>
        /// Tokenizes the expression as per the license expression rules. Throws if the string contains invalid characters.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static IEnumerable<LicenseExpressionToken> GetTokens(string expression)
        {
            var tokenizer = new LicenseExpressionTokenizer(expression);
            if (!tokenizer.HasValidCharacters())
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidCharacters, expression));
            }

            var tokens = tokenizer.Tokenize();
            return tokens;
        }

        private static void ProcessOperators(Stack<LicenseExpressionToken> operatorStack, Stack<Tuple<bool, object>> operandStack)
        {
            var op = operatorStack.Pop();
            if (op.TokenType == LicenseTokenType.WITH)
            {
                var rightExp = PopIfNotEmpty(operandStack);
                var leftExp = PopIfNotEmpty(operandStack);

                if (!(rightExp.Item1 == leftExp.Item1 == true))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExpression));

                }
                var right = rightExp.Item2 as LicenseExpressionToken;
                var left = leftExp.Item2 as LicenseExpressionToken;

                var withNode = new WithOperator(NuGetLicense.Parse(left.Value), NuGetLicenseException.Parse(right.Value));

                operandStack.Push(new Tuple<bool, object>(false, withNode));
            }
            else
            {
                var logicalOperator = op.TokenType == LicenseTokenType.AND ? LogicalOperatorType.And : LogicalOperatorType.Or;

                var rightExp = PopIfNotEmpty(operandStack);
                var leftExp = PopIfNotEmpty(operandStack);

                var right = rightExp.Item1 ?
                    NuGetLicense.Parse(((LicenseExpressionToken)rightExp.Item2).Value) :
                    (NuGetLicenseExpression)rightExp.Item2;

                var left = leftExp.Item1 ?
                    NuGetLicense.Parse(((LicenseExpressionToken)leftExp.Item2).Value) :
                    (NuGetLicenseExpression)leftExp.Item2;

                var newExpression = new LogicalOperator(logicalOperator, left, right);
                operandStack.Push(new Tuple<bool, object>(false, newExpression));
            }
        }

        private static Tuple<bool, object> PopIfNotEmpty(Stack<Tuple<bool, object>> operandStack)
        {
            return operandStack.Count > 0 ?
                operandStack.Pop() :
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExpression));
        }
    }
}