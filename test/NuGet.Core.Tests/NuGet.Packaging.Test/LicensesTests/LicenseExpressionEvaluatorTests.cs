// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class LicenseExpressionEvaluatorTests
    {
        // TODO NK - Add tests with plus
        // TODO NK - Add tests that try to mess with the precedence of operators
        [Theory]
        [InlineData("MIT OR LPL-1.0", "MIT OR LPL-1.0", "OR")]
        [InlineData("MIT AND LPL-1.0", "MIT AND LPL-1.0", "AND")]
        [InlineData("MIT WITH LPL-1.0", "MIT WITH LPL-1.0", "WITH")]
        [InlineData("MIT OR (GPL-1.0 WITH E8)", "GPL-1.0 WITH E8 OR MIT", "OR")]
        [InlineData("(GPL-1.0 WITH E8) OR MIT", "GPL-1.0 WITH E8 OR MIT", "OR")]
        [InlineData("MIT OR GPL-1.0 WITH E8 AND APACHE-2.0", "GPL-1.0 WITH E8 AND APACHE-2.0 OR MIT", "OR")]
        [InlineData("MIT OR GPL-1.0 WITH E8", "GPL-1.0 WITH E8 OR MIT", "OR")]
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE)", "LGPL-2.1 AND BSD-2-CLAUSE", "AND")]
        [InlineData("MIT OR (LPL-1.0 OR GPL-1.0 WITH E8)", "GPL-1.0 WITH E8 OR LPL-1.0 OR MIT", "OR")]
        [InlineData("MIT OR (GPL-1.0 WITH E8 OR LPL-1.0)", "GPL-1.0 WITH E8 OR LPL-1.0 OR MIT", "OR")]
        [InlineData("MIT WITH E8 AND GPL-1.0 OR LPL-1.0", "MIT WITH E8 AND GPL-1.0 OR LPL-1.0", "OR")]
        [InlineData("MIT OR GPL-1.0 AND LPL-1.0 WITH E8", "LPL-1.0 WITH E8 AND GPL-1.0 OR MIT", "OR")]
        [InlineData("MIT OR ((GPL-1.0 WITH E8) AND APACHE-2.0)", "GPL-1.0 WITH E8 AND APACHE-2.0 OR MIT", "OR")]
        [InlineData("((MIT) AND (LPL-1.0))", "MIT AND LPL-1.0", "AND")]
        [InlineData("((( (LGPL-2.1) AND BSD-2-CLAUSE)))", "LGPL-2.1 AND BSD-2-CLAUSE", "AND")]
        [InlineData("MIT OR (LPL-1.0 OR (GPL-1.0 WITH E8))", "GPL-1.0 WITH E8 OR LPL-1.0 OR MIT", "OR")]
        [InlineData("MIT OR (LPL-1.0 AND (GPL-1.0 WITH E8) AND LGPL-2.1)", "GPL-1.0 WITH E8 AND LPL-1.0 AND LGPL-2.1 OR MIT", "OR")]
        [InlineData("MIT OR ((GPL-1.0 WITH E8) AND LPL-1.0)", "GPL-1.0 WITH E8 AND LPL-1.0 OR MIT", "OR")]
        [InlineData("MIT AND (GPL-1.0 WITH E8 OR APACHE-2.0)", "GPL-1.0 WITH E8 OR APACHE-2.0 AND MIT", "AND")] // This expr is a weird legal expression, but a good test for the exp parser.

        public void LicenseExpressionEvaluator_EvaluatesComplexExpression(string infix, string postfix, string rootOperator)
        {
            var postfixExpression = LicenseExpressionEvaluator.Evaluate(new LicenseExpressionTokenizer(infix).Tokenize().ToArray());
            Assert.Equal(postfix, postfixExpression.ToString());

            var success = Enum.TryParse<LogicalOperatorType>(rootOperator, true, out var logicalOperator);

            if (success)
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
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE")]
        [InlineData("MIT AND LPL-1.0)")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT  () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH E8 OR LPL-1.0)")]
        [InlineData("A WITH B C WITH D")]
        public void LicenseExpressionEvaluator_EvaluateThrowsWhenBracketsDoNotMatch(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionEvaluator.Evaluate(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }

        [Theory]
        [InlineData("MIT OR (GPL-1.0 E8 WITH)")]
        [InlineData("MIT LPL-1.0 GPL-1.0 E8 WITH OR OR")]
        [InlineData("OR MIT (GPL-1.0 E8 WITH)")]
        [InlineData("MIT WITH GPL-1.0 WITH E8 WITH LPL-1.0")]
        [InlineData("A WITH B C WITH D")]
        [InlineData("A WITH B C WITH D OR E")]
        public void LicenseExpressionEvaluator_EvaluateThrowsIfExpressionIsInvalid(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionEvaluator.Evaluate(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }
    }
}
