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
        [Theory]
        [InlineData("MIT OR LPL-1.0", "MIT LPL-1.0 OR")]
        [InlineData("MIT AND LPL-1.0", "MIT LPL-1.0 AND")]
        [InlineData("MIT WITH LPL-1.0", "MIT LPL-1.0 WITH")]
        [InlineData("MIT OR (GPL-1.0 WITH E8)", "MIT GPL-1.0 E8 WITH OR")]
        [InlineData("MIT OR GPL-1.0 WITH E8 AND APACHE-2.0", "MIT GPL-1.0 E8 WITH APACHE-2.0 AND OR")]
        [InlineData("MIT OR GPL-1.0 WITH E8", "MIT GPL-1.0 E8 WITH OR")]
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE)", "LGPL-2.1 BSD-2-CLAUSE AND")]
        [InlineData("MIT OR (LPL-1.0 OR GPL-1.0 WITH E8)", "MIT LPL-1.0 GPL-1.0 E8 WITH OR OR")]
        [InlineData("MIT OR (GPL-1.0 WITH E8 OR LPL-1.0)", "MIT GPL-1.0 E8 WITH LPL-1.0 OR OR")]
        [InlineData("MIT WITH E8 AND GP-1.0 OR LPL-1.0", "MIT E8 WITH GP-1.0 AND LPL-1.0 OR")]
        [InlineData("MIT OR GPL-1.0 AND LPL-1.0 WITH E8", "MIT GPL-1.0 LPL-1.0 E8 WITH AND OR")]
        //[InlineData("MIT OR ((GPL-1.0 WITH E8) AND APACHE-2.0)", "MIT GPL-1.0 E8 WITH APACHE-2.0 AND OR")]
        //[InlineData("((MIT) AND (LPL-1.0))", "MIT LPL-1.0 AND")]
        //[InlineData("((( (LGPL-2.1) AND BSD-2-CLAUSE)))", "LGPL-2.1 BSD-2-CLAUSE AND")]
        //[InlineData("MIT OR (LPL-1.0 OR (GPL-1.0 WITH E8))", "MIT LPL-1.0 GPL-1.0 E8 WITH OR OR")]//[InlineData("MIT OR (LPL-1.0 AND (GPL-1.0 WITH E8) AND LGPL-2.1)", "MIT LPL-1.0 GPL-1.0 E8 WITH AND LGPL-2.1 AND OR")]
        //[InlineData("MIT OR ((GPL-1.0 WITH E8) AND LPL-1.0)", "MIT GPL-1.0 E8 WITH LPL-1.0 AND OR")]

        public void LicenseExpressionEvaluator_ConvertsComplexExpressions(string infix, string postfix)
        {
            var postfixExpression = LicenseExpressionEvaluator.ConvertInfixToPostfixLicenseExpression(new LicenseExpressionTokenizer(infix).Tokenize().ToArray());
            Assert.Equal(postfix, string.Join(" ", postfixExpression.Select(e => e.Value)));
        }

        [Theory]
        [InlineData("(LGPL-2.1 AND BSD-2-CLAUSE")]
        [InlineData("MIT AND LPL-1.0)")]
        [InlineData("MIT (AND) LPL-1.0")]
        [InlineData("((MIT) (AND) (LPL-1.0))")]
        [InlineData("MIT (AND LPL-1.0)")]
        [InlineData("MIT  () OR LPL-1.0")]
        [InlineData("MIT OR GPL-1.0 (WITH E8 OR LPL-1.0 )")]
        public void LicenseExpressionEvaluator_ThrowsWhenBracketsDoNotMatch(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionEvaluator.ConvertInfixToPostfixLicenseExpression(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }


        // TODO NK - Add tests with plus
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
            else if (rootOperator.Equals("with", StringComparison.OrdinalIgnoreCase))
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
        public void LicenseExpressionEvaluator_EvaluateThrowsWhenBracketsDoNotMatch(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionEvaluator.Evaluate(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }

        [Theory]
        [InlineData("MIT OR (GPL-1.0 E8 WITH)")]
        [InlineData("MIT LPL-1.0 GPL-1.0 E8 WITH OR OR")]
        [InlineData("OR MIT (GPL-1.0 E8 WITH)")]
        [InlineData("MIT WITH GPL-1.0 WITH E8 WITH LPL-1.0")]
        public void LicenseExpressionEvaluator_EvaluateNotInfixExpressionThrows(string infix)
        {
            Assert.Throws<ArgumentException>(() => LicenseExpressionEvaluator.Evaluate(new LicenseExpressionTokenizer(infix).Tokenize().ToArray()));
        }
    }
}
