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
        // TODO NK - on the exception catching. Verify the correct is being thrown.
        // TODO NK - Make license ids conform to the specification.
        // TODO NK - add tests for casing of the license iDs and casing of the operators
        // TODO NK - The parsing of custom/weird licenses can be improved. As in make sure they conform to the standard ID.
        [Theory]
        [InlineData("MIT OR LPL-1.0", "MIT OR LPL-1.0", "OR", true)]
        [InlineData("MIT AND LPL-1.0", "MIT AND LPL-1.0", "AND", true)]
        [InlineData("MIT OR (AFL-1.1 WITH 389-exception)", "AFL-1.1 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("(AFL-1.1 WITH 389-exception) OR MIT", "AFL-1.1 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("MIT OR AFL-1.1 WITH 389-exception AND Apache-2.0", "AFL-1.1 WITH 389-exception AND Apache-2.0 OR MIT", "OR", true)]
        [InlineData("MIT OR AFL-1.1 WITH 389-exception", "AFL-1.1 WITH 389-exception OR MIT", "OR", true)]
        [InlineData("MIT OR (LPL-1.0 OR AFL-1.1 WITH 389-exception)", "AFL-1.1 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT OR (AFL-1.1 WITH 389-exception OR LPL-1.0)", "AFL-1.1 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT WITH 389-exception AND AFL-1.1 OR LPL-1.0", "MIT WITH 389-exception AND AFL-1.1 OR LPL-1.0", "OR", true)]
        [InlineData("MIT OR AFL-1.1 AND LPL-1.0 WITH 389-exception", "LPL-1.0 WITH 389-exception AND AFL-1.1 OR MIT", "OR", true)]
        [InlineData("MIT OR ((AFL-1.1 WITH 389-exception) AND Apache-2.0)", "AFL-1.1 WITH 389-exception AND Apache-2.0 OR MIT", "OR", true)]
        [InlineData("((MIT) AND (LPL-1.0))", "MIT AND LPL-1.0", "AND", true)]
        [InlineData("MIT OR (LPL-1.0 OR (AFL-1.1 WITH 389-exception))", "AFL-1.1 WITH 389-exception OR LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT OR (LPL-1.0 AND (AFL-1.1 WITH 389-exception) AND AMDPLPA)", "AFL-1.1 WITH 389-exception AND LPL-1.0 AND AMDPLPA OR MIT", "OR", true)]
        [InlineData("MIT OR ((AFL-1.1 WITH 389-exception) AND LPL-1.0)", "AFL-1.1 WITH 389-exception AND LPL-1.0 OR MIT", "OR", true)]
        [InlineData("MIT AND (AFL-1.1 WITH 389-exception OR Apache-2.0)", "AFL-1.1 WITH 389-exception OR Apache-2.0 AND MIT", "AND", true)] // This expr is a weird legal expression, but a good test for the exp parser.
        [InlineData("(AFL-1.1+ WITH 389-exception) OR MIT", "AFL-1.1+ WITH 389-exception OR MIT", "OR", true)]
        [InlineData("MIT OR LPL-1.0+", "MIT OR LPL-1.0+", "OR", true)]
        [InlineData("(AMDPLPA AND BSD-2-Clause)", "AMDPLPA AND BSD-2-Clause", "AND", true)]
        [InlineData("((( (AMDPLPA) AND BSD-2-Clause)))", "AMDPLPA AND BSD-2-Clause", "AND", true)]
        [InlineData("(And+) AND or", "And+ AND or", "AND", false)]
        public void LicenseExpressionParser_ParsesComplexExpression(string infix, string postfix, string rootOperator, bool hasStandardIdentifiers)
        {
            var licenseExpression = LicenseExpressionParser.Parse(infix);
            Assert.Equal(postfix, licenseExpression.ToString());
            Assert.Equal(licenseExpression.HasOnlyStandardIdentifiers(), hasStandardIdentifiers);

            if (Enum.TryParse<LogicalOperatorType>(rootOperator, true, out var logicalOperator))
            {
                var expression = licenseExpression as LogicalOperator;
                Assert.Equal(expression.LogicalOperatorType, logicalOperator);
            }
            else if (rootOperator.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            {
                var expression = licenseExpression as WithOperator;
                Assert.NotNull(expression);
            }
            else
            {
                Assert.Equal(rootOperator, licenseExpression.ToString());
            }
        }

        [Theory]
        [InlineData("MIT WITH LPL-1.0")] // Both identifiers are licenses
        [InlineData("mif-exception WITH Classpath-exception-2.0")] // Both identifiers are exceptions
        public void LicenseExpressionParser_ThrowsForMismatchedArguments(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }


        [Theory]
        [InlineData("(GPL-1.0 WITH 389-exception) OR MIT")]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception AND Apache-2.0")]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception")]
        [InlineData("MIT OR (LPL-1.0 OR GPL-1.0 WITH 389-exception)")]
        [InlineData("MIT OR (GPL-1.0 WITH 389-exception OR LPL-1.0)")]
        [InlineData("(LGPL-2.1 AND BSD-2-Clause)")]
        [InlineData("((( (LGPL-2.1) AND BSD-2-Clause)))")]
        public void LicenseExpressionParser_ComplexExpressionWithDeprecatedIdentifiersThrows(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
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
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }

        [Theory]
        [InlineData("A AND ")]
        [InlineData("(A AND B")]
        [InlineData("A AND B)")]
        [InlineData("A (AND) B")]
        [InlineData("((A) (AND) (B))")]
        [InlineData("A (AND B)")]
        [InlineData("A () OR B")]
        [InlineData("A OR C (WITH 389-exception OR B)")]
        [InlineData("A WITH B C WITH D")]
        public void LicenseExpressionParser_NonStrictParseThrowsForInvalidExpressions(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
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
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
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
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }
    }
}
