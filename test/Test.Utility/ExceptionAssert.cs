using System;
using System.IO;
using Xunit;

namespace NuGet.Test
{
    public static class ExceptionAssert
    {
        public static void Throws<TException>(Assert.ThrowsDelegate act) where TException : Exception
        {
            Throws<TException>(act, ex => { });
        }

        public static void Throws<TException>(Assert.ThrowsDelegate act, Action<TException> condition) where TException : Exception
        {
            Exception ex = Record.Exception(act);
            Assert.NotNull(ex);
            TException tex = Assert.IsAssignableFrom<TException>(ex);
            condition(tex);
        }

        public static void Throws<TException>(Assert.ThrowsDelegate action, string expectedMessage) where TException : Exception
        {
            Throws<TException>(action, ex => Assert.Equal(expectedMessage, ex.Message));
        }

        public static void ThrowsArgNull(Assert.ThrowsDelegate act, string paramName)
        {
            Throws<ArgumentNullException>(act, ex => Assert.Equal(paramName, ex.ParamName));
        }

        public static void ThrowsArgNullOrEmpty(Assert.ThrowsDelegate act, string paramName)
        {
            ThrowsArgumentException<ArgumentException>(act, paramName, "Value cannot be null or an empty string.");
        }

        public static void ThrowsArgOutOfRange(Assert.ThrowsDelegate act, string paramName, object minimum, object maximum, bool equalAllowed)
        {
            ThrowsArgumentException<ArgumentOutOfRangeException>(act, paramName, BuildOutOfRangeMessage(paramName, minimum, maximum, equalAllowed));
        }

        private static string BuildOutOfRangeMessage(string paramName, object minimum, object maximum, bool equalAllowed)
        {
            if (minimum == null)
            {
                return String.Format(equalAllowed ? "Argument_Must_Be_LessThanOrEqualTo" : "Argument_Must_Be_LessThan", maximum);
            }
            else if (maximum == null)
            {
                return String.Format(equalAllowed ? "Argument_Must_Be_GreaterThanOrEqualTo" : "Argument_Must_Be_GreaterThan", minimum);
            }
            else
            {
                return String.Format("Argument_Must_Be_Between", minimum, maximum);
            }
        }

        public static void ThrowsArgumentException(Assert.ThrowsDelegate act, string message)
        {
            ThrowsArgumentException<ArgumentException>(act, message);
        }

        public static void ThrowsArgumentException<TArgException>(Assert.ThrowsDelegate act, string message) where TArgException : ArgumentException
        {
            Throws<TArgException>(act, ex => Assert.Equal(message, ex.Message));
        }

        public static void ThrowsArgumentException(Assert.ThrowsDelegate act, string paramName, string message)
        {
            ThrowsArgumentException<ArgumentException>(act, paramName, message);
        }

        public static void ThrowsArgumentException<TArgException>(Assert.ThrowsDelegate act, string paramName, string message) where TArgException : ArgumentException
        {
            Throws<TArgException>(act, ex =>
            {
                Assert.Equal(paramName, ex.ParamName);
                var lines = ex.Message.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(2, lines.Length);
                Assert.Equal(message, lines[0]);
                Assert.True(lines[1].EndsWith(paramName));
            });
        }
    }
}
