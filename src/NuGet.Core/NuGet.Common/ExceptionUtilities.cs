// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace NuGet.Common
{
    /// <summary>
    /// For internal use only
    /// </summary>
    public static class ExceptionUtilities
    {
        public static string DisplayMessage(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            // use overloads
            var aggregate = exception as AggregateException;

            if (aggregate != null)
            {
                return DisplayMessage(aggregate);
            }

            var targetInvocation = exception as TargetInvocationException;

            if (targetInvocation != null)
            {
                return DisplayMessage(targetInvocation);
            }

            // fall back to simply exploring all inner exceptions
            return JoinMessages(GetMessages(exception));
        }

        public static string DisplayMessage(AggregateException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return JoinMessages(GetMessages(exception));
        }

        public static string DisplayMessage(TargetInvocationException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return JoinMessages(GetMessages(exception));
        }

        private static IEnumerable<string> GetMessages(AggregateException exception)
        {
            // try to avoid using the AggregateException message
            var inners = exception.Flatten().InnerExceptions;

            switch (inners?.Count)
            {
                case null:
                case 0:
                    yield return exception.Message;

                    break;

                default:
                    foreach (var inner in inners)
                    {
                        foreach (var message in GetMessages(inner))
                        {
                            yield return message;
                        }
                    }

                    break;
            }
        }

        private static IEnumerable<string> GetMessages(TargetInvocationException exception)
        {
            // try to avoid using the TargetInvocationException message
            if (exception.InnerException != null)
            {
                return GetMessages(exception.InnerException);
            }

            return new[] { exception.Message };
        }

        private static IEnumerable<string> GetMessages(Exception exception)
        {
            Exception current = exception;
            string previous = null;
            while (current != null)
            {
                if (current.Message != null &&
                    current.Message != previous) // Ignore duplicate adjacent messages.
                {
                    previous = current.Message;
                    yield return current.Message;
                }

                current = current.InnerException;
            }
        }

        private static IEnumerable<string> GetLines(string input)
        {
            using (var reader = new StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static string JoinMessages(IEnumerable<string> messages)
        {
            var builder = new StringBuilder();
            foreach (var message in messages)
            {
                // indent all but the first message
                bool indent = builder.Length > 0;

                foreach (var line in GetLines(message))
                {
                    if (indent)
                    {
                        builder.Append("  ");
                    }

                    builder.AppendLine(line);
                }
            }

            return builder.ToString().TrimEnd();
        }
    }
}
