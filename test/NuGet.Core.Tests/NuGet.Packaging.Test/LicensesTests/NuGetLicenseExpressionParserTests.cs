// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root flicense information.

using System;
using System.Globalization;

using Xunit;

namespace NuGet.Packaging.Licenses.Test
{
    public class NuGetLicenseExpressionParserTests
    {
        [Theory]
        [InlineData("MIT OR LPL-1.0", "OR", true)]
        [InlineData("MIT AND LPL-1.0", "AND", true)]
        [InlineData("MIT OR (AFL-1.1 WITH 389-exception)", "OR", true)]
        [InlineData("(AFL-1.1 WITH 389-exception) OR MIT", "OR", true)]
        [InlineData("MIT OR AFL-1.1 WITH 389-exception AND Apache-2.0", "OR", true)]
        [InlineData("MIT OR AFL-1.1 WITH 389-exception", "OR", true)]
        [InlineData("MIT OR (LPL-1.0 OR AFL-1.1 WITH 389-exception)", "OR", true)]
        [InlineData("MIT OR (AFL-1.1 WITH 389-exception OR LPL-1.0)", "OR", true)]
        [InlineData("MIT WITH 389-exception AND AFL-1.1 OR LPL-1.0", "OR", true)]
        [InlineData("MIT OR AFL-1.1 AND LPL-1.0 WITH 389-exception", "OR", true)]
        [InlineData("MIT OR ((AFL-1.1 WITH 389-exception) AND Apache-2.0)", "OR", true)]
        [InlineData("((MIT) AND (LPL-1.0))", "AND", true)]
        [InlineData("MIT OR (LPL-1.0 OR (AFL-1.1 WITH 389-exception))", "OR", true)]
        [InlineData("MIT OR (LPL-1.0 AND (AFL-1.1 WITH 389-exception) AND AMDPLPA)", "OR", true)]
        [InlineData("MIT OR ((AFL-1.1 WITH 389-exception) AND LPL-1.0)", "OR", true)]
        [InlineData("MIT AND (AFL-1.1 WITH 389-exception OR Apache-2.0)", "AND", true)] // here. This expr is a weird legal expression, but a good test for the exp parser.
        [InlineData("(AFL-1.1+ WITH 389-exception) OR MIT", "OR", true)]
        [InlineData("MIT OR LPL-1.0+", "OR", true)]
        [InlineData("(AMDPLPA AND BSD-2-Clause)", "AND", true)]
        [InlineData("((( (AMDPLPA) AND BSD-2-Clause)))", "AND", true)]
        [InlineData("(And+) AND or", "AND", false)]
        [InlineData("(AMDPLPA AND BSD-2-Clause) AND (MIT WITH 389-exception)", "AND", true)]
        [InlineData("(AMDPLPA AND BSD-2-Clause) AND (MIT OR Apache-2.0)", "AND", true)]

        public void LicenseExpressionParser_ParsesComplexExpression(string expression, string rootOperator, bool hasStandardIdentifiers)
        {
            var licenseExpression = NuGetLicenseExpressionParser.Parse(expression);
            Assert.Equal(expression.Replace("(", "").Replace(")", "").Trim(), licenseExpression.ToString());

            Assert.Equal(licenseExpression.HasOnlyStandardIdentifiers(), hasStandardIdentifiers);

            if (Enum.TryParse<LogicalOperatorType>(rootOperator, true, out var logicalOperator))
            {
                var exp = licenseExpression as LogicalOperator;
                Assert.Equal(exp.LogicalOperatorType, logicalOperator);
            }
            else if (rootOperator.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            {
                var exp = licenseExpression as WithOperator;
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
        public void LicenseExpressionParser_ParsesSimpleExpression(string expression, bool hasStandardIdentifiers, bool hasPlus)
        {
            var licenseExpression = NuGetLicenseExpressionParser.Parse(expression);
            Assert.Equal(expression, licenseExpression.ToString());
            Assert.Equal(licenseExpression.HasOnlyStandardIdentifiers(), hasStandardIdentifiers);
            Assert.NotNull(licenseExpression as NuGetLicense);
            Assert.Equal(hasPlus, (licenseExpression as NuGetLicense).Plus);

        }

        [Theory]
        [InlineData("MIT WITH LPL-1.0", "LPL-1.0", false)] // Both identifiers are licenses
        [InlineData("mif-exception WITH Classpath-exception-2.0", "mif-exception", true)] // Both identifiers are exceptions
        public void LicenseExpressionParser_ThrowsForMismatchedArguments(string expression, string badIdentifier, bool IsExceptionAsLicense)
        {
            var ex = Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
            if (IsExceptionAsLicense)
            {
                Assert.Equal(ex.Message, string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_LicenseIdentifierIsException, badIdentifier));
            }
            else
            {
                Assert.Equal(ex.Message, string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_ExceptionIdentifierIsLicense, badIdentifier));
            }
        }

        [Theory]
        [InlineData("(GPL-1.0 WITH 389-exception) OR MIT", "GPL-1.0")]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception AND Apache-2.0", "GPL-1.0")]
        [InlineData("MIT OR GPL-1.0 WITH 389-exception", "GPL-1.0")]
        [InlineData("MIT OR (LPL-1.0 OR GPL-1.0 WITH 389-exception)", "GPL-1.0")]
        [InlineData("MIT OR (GPL-1.0 WITH 389-exception OR LPL-1.0)", "GPL-1.0")]
        [InlineData("(LGPL-2.1 AND BSD-2-Clause)", "LGPL-2.1")]
        [InlineData("((( (LGPL-2.1) AND BSD-2-Clause)))", "LGPL-2.1")]
        public void LicenseExpressionParser_ComplexExpressionWithDeprecatedIdentifiersThrows(string expression, string deprecatedValue)
        {
            var ex = Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_DeprecatedIdentifier, deprecatedValue), ex.Message);
        }

        [Theory]
        [InlineData("MIT AND ")]
        [InlineData("(MIT AND BSD-2-Clause")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH 389-exception OR LPL-1.0)")]
        public void LicenseExpressionParser_StrictParseThrowsForInvalidExpressions(string expression)
        {
            Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
        }

        [Theory]
        [InlineData("MIT and BSD-2-Clause")]
        [InlineData("MIT or BSD-2-Clause")]
        [InlineData("MIT with Classpath-exception-2.0")]
        public void LicenseExpressionParser_ThrowsForExpressionsWithBadCasing(string expression)
        {
            Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
        }

        [Theory]
        [InlineData("MIT WITH classpath-exception-2.0", "classpath-exception-2.0")]
        public void LicenseExpressionParser_ThrowsForInvalidExceptionDueToBadCasing(string expression, string exception)
        {
            var ex = Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExceptionIdentifier, exception), ex.Message);
        }

        [Theory]
        [InlineData("MIt WITH Classpath-exception-2.0", false, true)]
        [InlineData("MIt AND BSD-2-Clause", false, true)]
        [InlineData("MIt OR BSD-2-Clause", false, true)]
        public void LicenseExpressionParser_CreatesNonStandardExpressionsWithBadCasing(string expression, bool isFirstOperatorStandard, bool isSecondOperatorStandard)
        {
            var licenseExpression = NuGetLicenseExpressionParser.Parse(expression);
            var withExpression = licenseExpression as WithOperator;
            var logicalExpression = licenseExpression as LogicalOperator;

            if (withExpression != null)
            {
                Assert.Equal(isFirstOperatorStandard, withExpression.License.IsStandardLicense);
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
        [InlineData(")A( AND B")]
        [InlineData("A( AND )B")]
        public void LicenseExpressionParser_ThrowsForInvalidExpressions(string expression)
        {
            Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
        }

        [Theory]
        [InlineData("(GPL-1.0+ WITH Classpath-exception-2.0) OR MIT!")]
        [InlineData("(GPL-1.0+ WITH Classpath-exception-2.0) OR MIT@")]
        [InlineData("(GPL-1.0+ WITH Classpath-exception-2.0) OR MIT/")]
        [InlineData("(GPL-1.0+ WITH Classpath-exception-2.0) OR MIT[]")]
        [InlineData("      (GPL-1.0+ WITH Classpath-exception-2.0) OR MIT[] ")]
        public void LicenseExpressionParser_ParseThrowsForInvalidCharactersInExpression(string expression)
        {
            var ex = Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidCharacters, expression), ex.Message);
        }

        [Theory]
        [InlineData("LGPL-2.1 AND ")]
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE")]
        [InlineData("MIT AND LPL-1.0)")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT  () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH Classpath-exception-2.0 OR LPL-1.0)")]
        [InlineData("A WITH B C WITH D")]
        public void LicenseExpressionParser_EvaluateThrowsWhenBracketsDoNotMatch(string expression)
        {
            Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
        }

        [Theory]
        [InlineData("MIT OR (GPL-1.0 Classpath-exception-2.0 WITH)")]
        [InlineData("MIT LPL-1.0 GPL-1.0 Classpath-exception-2.0 WITH OR OR")]
        [InlineData("OR MIT (GPL-1.0 Classpath-exception-2.0 WITH)")]
        [InlineData("MIT WITH GPL-1.0 WITH Classpath-exception-2.0 WITH LPL-1.0")]
        [InlineData("A WITH B C WITH D")]
        [InlineData("A WITH B C WITH D OR E")]
        public void LicenseExpressionParser_NotStrict_EvaluateThrowsIfExpressionIsInvalid(string expression)
        {
            Assert.Throws<ArgumentException>(() => NuGetLicenseExpressionParser.Parse(expression));
        }
    }
}
