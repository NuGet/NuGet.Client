// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.Packaging.Licenses
{
    public class LicenseExpressionEvaluator
    {
        /// <summary>
        /// Based on the Shunting Yard algorithm. <see href="https://en.wikipedia.org/wiki/Shunting-yard_algorithm"/>
        /// </summary>
        /// <param name="infixTokens"></param>
        /// <returns></returns>
        public static NuGetLicenseExpression Evaluate(LicenseExpressionToken[] infixTokens)
        {
            var operatorStack = new Stack<LicenseExpressionToken>();
            var operandStack = new Stack<LicenseExpressionToken>();
            NuGetLicenseExpression leftHandSideExpression = null;
            NuGetLicenseExpression rightHandSideExpression = null;

            var lastTokenType = LicenseTokenType.VALUE;
            var firstPass = true;

            foreach (var token in infixTokens)
            {
                var currentTokenType = token.TokenType;
                switch (token.TokenType)
                {
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
                            ProcessOperators(operatorStack, operandStack, ref leftHandSideExpression, ref rightHandSideExpression);
                        }

                        if (operatorStack.Count > 0)
                        {
                            // pop the bracket
                            operatorStack.Pop();
                        }
                        else
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_MismatchedParenthesis));
                        }
                        break;

                    case LicenseTokenType.VALUE:
                        if (!firstPass && !token.TokenType.IsValidPrecedingToken(lastTokenType))
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidToken, token.Value));
                        }
                        // Add it to the operandstack. Only add it to the expression when you meet an operator
                        operandStack.Push(token);
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
                        else if (token.TokenType >= operatorStack.Peek().TokenType) // TODO NK - safeguard for overpopping here. Don't 
                        {
                            ProcessOperators(operatorStack, operandStack, ref leftHandSideExpression, ref rightHandSideExpression);
                            operatorStack.Push(token);
                        }
                        break;
                    default:
                        throw new ArgumentException("Should not happen. File a bug on NuGet/Home if seen.");
                }
                lastTokenType = currentTokenType;
                firstPass = false;
            }

            while (operatorStack.Count > 0)
            {
                if (operatorStack.Peek().TokenType != LicenseTokenType.OPENING_BRACKET)
                {
                    ProcessOperators(operatorStack, operandStack, ref leftHandSideExpression, ref rightHandSideExpression);
                }
                else
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_MismatchedParenthesis));
                }
            }

            return rightHandSideExpression == null ? leftHandSideExpression : null; //TODO NK - A WITH B C WITH D
        }

        private static void ProcessOperators(Stack<LicenseExpressionToken> operatorStack, Stack<LicenseExpressionToken> operandStack, ref NuGetLicenseExpression leftHandSideExpression, ref NuGetLicenseExpression rightHandSideExpression)
        {
            var op = operatorStack.Pop();
            if (op.TokenType == LicenseTokenType.WITH)
            {
                var right = PopIfNotEmpty(operandStack);
                var left = PopIfNotEmpty(operandStack);

                var withNode = new WithOperator(NuGetLicense.Parse(left.Value), NuGetLicenseException.Parse(right.Value));

                if (leftHandSideExpression == null)
                {
                    leftHandSideExpression = withNode;
                }
                else if (rightHandSideExpression == null)
                {
                    rightHandSideExpression = withNode;
                }
                else
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExpression));//  TODO NK - add tests ex. A WITH B C WITH D OR E
                }
            }
            else
            {
                var logicalOperator = op.TokenType == LicenseTokenType.AND ? LogicalOperatorType.AND : LogicalOperatorType.OR;

                if (leftHandSideExpression == null && rightHandSideExpression == null)
                {
                    var right = PopIfNotEmpty(operandStack);
                    var left = PopIfNotEmpty(operandStack);
                    leftHandSideExpression = new LogicalOperator(logicalOperator, NuGetLicense.Parse(left.Value), NuGetLicense.Parse(right.Value));
                }
                else if (rightHandSideExpression == null)
                {
                    var right = PopIfNotEmpty(operandStack);
                    var newExpression = new LogicalOperator(logicalOperator, leftHandSideExpression, NuGetLicense.Parse(right.Value));
                    leftHandSideExpression = newExpression;
                }
                else if (leftHandSideExpression == null)
                {
                    throw new ArgumentException("Should not happen. File a bug on NuGet/Home if seen.");
                }
                else
                {
                    var newExpression = new LogicalOperator(logicalOperator, leftHandSideExpression, rightHandSideExpression);
                    rightHandSideExpression = null;
                    leftHandSideExpression = newExpression;
                }
            }
        }

        private static LicenseExpressionToken PopIfNotEmpty(Stack<LicenseExpressionToken> stack)
        {
            return stack.Count > 0 ?
                stack.Pop() :
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExpression));
        }
    }
}

