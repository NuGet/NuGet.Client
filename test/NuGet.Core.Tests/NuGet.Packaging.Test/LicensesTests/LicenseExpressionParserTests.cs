// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class LicenseExpressionParserTests
    {
        // TODO NK - Make license ids conform to the specification.
        // TODO NK - add tests for casing of the license iDs and casing of the operators
        // TODO NK - Should strict parse the deprecated exceptions?
        // TODO NK - maybe we have different handling for the deprecated IDs.

        [Theory]
        [InlineData("MIT OR LPL-1.0", "MIT OR LPL-1.0", "OR", true)]
        [InlineData("MIT AND LPL-1.0", "MIT AND LPL-1.0", "AND", true)]
        [InlineData("MIT WITH LPL-1.0", "MIT WITH LPL-1.0", "WITH", true)]
        [InlineData("MIT OR (GPL-1.0 WITH 389-exception)", "GPL-1.0 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("(GPL-1.0 WITH 389-exception) OR MIT", "GPL-1.0 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception AND Apache-2.0", "GPL-1.0 WITH 389-exception AND Apache-2.0 OR MIT", "OR", true)]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception", "GPL-1.0 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("(LGPL-2.1 AND BSD-2-Clause)", "LGPL-2.1 AND BSD-2-Clause", "AND", true)]
        [InlineData("MIT OR (LPL-1.0 OR GPL-1.0 WITH 389-exception)", "GPL-1.0 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT OR (GPL-1.0 WITH 389-exception OR LPL-1.0)", "GPL-1.0 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT WITH 389-exception AND GPL-1.0 OR LPL-1.0", "MIT WITH 389-exception AND GPL-1.0 OR LPL-1.0", "OR", true)]
        [InlineData("MIT OR GPL-1.0 AND LPL-1.0 WITH 389-exception", "LPL-1.0 WITH 389-exception AND GPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT OR ((GPL-1.0 WITH 389-exception) AND Apache-2.0)", "GPL-1.0 WITH 389-exception AND Apache-2.0 OR MIT", "OR", true)]
        [InlineData("((MIT) AND (LPL-1.0))", "MIT AND LPL-1.0", "AND", true)]
        [InlineData("((( (LGPL-2.1) AND BSD-2-Clause)))", "LGPL-2.1 AND BSD-2-Clause", "AND", true)]
        [InlineData("MIT OR (LPL-1.0 OR (GPL-1.0 WITH 389-exception))", "GPL-1.0 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT OR (LPL-1.0 AND (GPL-1.0 WITH 389-exception) AND LGPL-2.1)", "GPL-1.0 WITH 389-exception AND LPL-1.0 AND LGPL-2.1 OR MIT", "OR", true)]
        [InlineData("MIT OR ((GPL-1.0 WITH 389-exception) AND LPL-1.0)", "GPL-1.0 WITH 389-exception AND LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT AND (GPL-1.0 WITH 389-exception OR Apache-2.0)", "GPL-1.0 WITH 389-exception OR Apache-2.0 AND MIT", "AND", true)] // This expr is a weird legal expression, but a good test for the exp parser.
        [InlineData("(GPL-1.0+ WITH 389-exception) OR MIT", "GPL-1.0+ WITH 389-exception OR MIT", "OR", true)]
        [InlineData("MIT OR LPL-1.0+", "MIT OR LPL-1.0+", "OR", true)]
        // Separate strict and non strict parsing
        [InlineData("(And+) AND or", "And+ AND or", "AND", false)]

        public void LicenseExpressionParser_ParsesComplexExpression(string infix, string postfix, string rootOperator, bool hasNonStandardIdentifiers)
        {
            var postfixExpression = LicenseExpressionParser.Parse(infix);

            //TODO NK - walk and find the non standard identifiers. hasNonStandardIdentifiers
            Assert.Equal(postfix, postfixExpression.ToString());

            if (Enum.TryParse<LogicalOperatorType>(rootOperator, true, out var logicalOperator))
            {
                var expression = postfixExpression as LogicalOperator;
                Assert.Equal(expression.LogicalOperatorType, logicalOperator);
            }
            else if (rootOperator.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            {
                var expression = postfixExpression as WithOperator;
                Assert.NotNull(expression);
            }
            else
            {
                Assert.Equal(rootOperator, postfixExpression.ToString());
            }
        }


        [Theory]
        [InlineData("LGPL-2.1 AND ")]
        [InlineData("(LGPL-2.1 AND BSD-2-Clause")]
        [InlineData("MIT AND LPL-1.0)")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH 389-exception OR LPL-1.0)")]

        public void LicenseExpressionParser_StrictParseThrowsForInvalidExpressions(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix, strict: true));
        }

        [Theory]
        [InlineData("A AND ")]
        [InlineData("(A AND BSD-2-Clause")]
        [InlineData("A AND B)")]
        [InlineData("A (AND) B")]
        [InlineData("((A) (AND) (B))")]
        [InlineData("A (AND B)")]
        [InlineData("A () OR B")]
        [InlineData("A OR C (WITH 389-exception OR B)")]
        [InlineData("A WITH B C WITH D")]
        public void LicenseExpressionParser_NonStrictParseThrowsForInvalidExpressions(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix, strict: false));
        }

        [Theory]
        [InlineData("(GPL-1.0+ WITH E8) OR MIT!")]
        [InlineData("(GPL-1.0+ WITH E8) OR MIT@")]
        [InlineData("(GPL-1.0+ WITH E8) OR MIT/")]
        [InlineData("(GPL-1.0+ WITH E8) OR MIT[]")]
        public void LicenseExpressionParser_ParseThrowsForInvalidCharactersInExpression(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }

        [Theory]
        [InlineData("LGPL-2.1 AND ")]
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE")]
        [InlineData("MIT AND LPL-1.0)")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT  () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH E8 OR LPL-1.0)")]
        [InlineData("A WITH B C WITH D")]
        public void LicenseExpressionParser_EvaluateThrowsWhenBracketsDoNotMatch(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(new LicenseExpressionTokenizer(infix).Tokenize().ToArray(), strict: false));
        }

        [Theory]
        [InlineData("MIT OR (GPL-1.0 E8 WITH)")]
        [InlineData("MIT LPL-1.0 GPL-1.0 E8 WITH OR OR")]
        [InlineData("OR MIT (GPL-1.0 E8 WITH)")]
        [InlineData("MIT WITH GPL-1.0 WITH E8 WITH LPL-1.0")]
        [InlineData("A WITH B C WITH D")]
        [InlineData("A WITH B C WITH D OR E")]
        public void LicenseExpressionParser_NotStrict_EvaluateThrowsIfExpressionIsInvalid(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(new LicenseExpressionTokenizer(infix).Tokenize().ToArray(), strict: false));
        }
    }
}
