using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace NuGet.Common.Test
{
    public class ExceptionUtilitiesTests
    {
        [Fact]
        public void ExceptionUtilities_AggregateException()
        {
            /* Arrange
             * Exceptions:
             *      B - E
             *    / 
             *  A - C 
             *    \ 
             *      D - F
             *        \ 
             *          G
             */
            var g = new Exception("G");
            var f = new Exception("F");
            var e = new Exception("E");
            var d = new AggregateException("D", f, g);
            var c = new Exception("C");
            var b = new Exception("B", e);
            var a = new AggregateException("A", b, c, d);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "B",
                "  E",
                "  C",
                "  F",
                "  G"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_AggregateExceptionWithoutInner()
        {
            /* Arrange
             * Exceptions:
             *  A
             */
            var a = new AggregateException("A");

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_TargetInvokationException()
        {
            /* Arrange
             * Exceptions:
             *  A - B - C
             */
            var c = new Exception("C");
            var b = new Exception("B", c);
            var a = new TargetInvocationException("A", b);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "B",
                "  C"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_TargetInvokationExceptionWithoutInner()
        {
            /* Arrange
             * Exceptions:
             *  A
             */
            var a = new TargetInvocationException("A", null);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_DoesNotIndentTwoLineMessage()
        {
            /* Arrange
             * Exceptions:
             *  A - B
             */
            var b = new Exception("B");
            var a = new Exception($"A1{Environment.NewLine}A2", b);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A1",
                "A2",
                "  B"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_SplitsLinesToMaintainIndent()
        {
            /* Arrange
             * Exceptions:
             *  A - B
             */
            var b = new Exception($"B1{Environment.NewLine}B2");
            var a = new Exception("A", b);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A",
                "  B1",
                "  B2"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_LeavesIndentAndWhitespaceInMessage()
        {
            /* Arrange
             * Exceptions:
             *  A - B
             */
            var b = new Exception($"B1{Environment.NewLine}  B2{Environment.NewLine}{Environment.NewLine}    B3");
            var a = new Exception("A", b);

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A",
                "  B1",
                "    B2",
                "  ",
                "      B3"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExceptionUtilities_ExceptionWithoutInner()
        {
            /* Arrange
             * Exceptions:
             *  A
             */
            var a = new Exception("A");

            // Act
            var message = ExceptionUtilities.DisplayMessage(a);

            // Assert
            var actual = GetLines(message);
            var expected = new[]
            {
                "A"
            };
            Assert.Equal(expected, actual);
        }

        private static string[] GetLines(string input)
        {
            var output = new List<string>();
            using (var reader = new StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    output.Add(line);
                }
            }

            return output.ToArray();
        }
    }
}
