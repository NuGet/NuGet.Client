// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root flicense information.

using System;
using System.Linq;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class LicenseExpressionParserTests
    {
        // TODO NK - on the exception catching. Verify the correct is being thrown.
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
        [InlineData("MIT", true, false)]
        [InlineData("AFL-1.1+", true, true)]
        [InlineData("MyFancyLicense", false, false)]
        public void LicenseExpressionParser_ParsesSimpleExpression(string infix, bool hasStandardIdentifiers, bool hasPlus)
        {
            var licenseExpression = LicenseExpressionParser.Parse(infix);
            Assert.Equal(infix, licenseExpression.ToString());
            Assert.Equal(licenseExpression.HasOnlyStandardIdentifiers(), hasStandardIdentifiers);
            Assert.NotNull(licenseExpression as NuGetLicense);
            Assert.Equal(hasPlus, (licenseExpression as NuGetLicense).Plus);

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
        [InlineData("MIT and BSD-2-Clause")]
        [InlineData("MIT or BSD-2-Clause")]
        [InlineData("MIT with Classpath-exception-2.0")]
        public void LicenseExpressionParser_ThrowsForExpressionsWithBadCasing(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }

        [Theory]
        [InlineData("MIT WITH classpath-exception-2.0")]
        public void LicenseExpressionParser_ThrowsForInvalidExceptionDueToBadCasing(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }

        [Theory]
        [InlineData("MIt WITH Classpath-exception-2.0", false, true)]
        [InlineData("MIt AND BSD-2-Clause", false, true)]
        [InlineData("MIt OR BSD-2-Clause", false, true)]
        public void LicenseExpressionParser_CreatesNonStandardExpressionsWithBadCasing(string infix, bool isFirstOperatorStandard, bool isSecondOperatorStandard)
        {
            var expression = LicenseExpressionParser.Parse(infix);
            var withExpression = expression as WithOperator;
            var logicalExpression = expression as LogicalOperator;

            if (withExpression != null)
            {
                Assert.Equal(isFirstOperatorStandard, withExpression.License.IsStandardLicense);
                Assert.Equal(isSecondOperatorStandard, withExpression.Exception.IsStandardException);
            }
            if (logicalExpression != null)
            {
                Assert.Equal(isFirstOperatorStandard, (logicalExpression.Left as NuGetLicense).IsStandardLicense);
                Assert.Equal(isSecondOperatorStandard, (logicalExpression.Right as NuGetLicense).IsStandardLicense);
            }
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
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
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
            Assert.Throws<ArgumentException>(() => LicenseExpressionParser.Parse(infix));
        }
    }
}
