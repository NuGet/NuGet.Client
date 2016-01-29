// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

            var aggregate = exception as AggregateException;

            if (aggregate != null)
            {
                return DisplayMessage(aggregate);
            }

            var target = exception as TargetInvocationException;

            if (target != null)
            {
                return DisplayMessage(target);
            }

            return exception.Message;
        }

        public static string DisplayMessage(AggregateException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var inners = exception.Flatten().InnerExceptions;

            switch (inners?.Count)
            {
                case null:
                case 0:
                    return exception.Message;
                case 1:
                    return DisplayMessage(inners[0]);
                default:
                    var builder = new StringBuilder();
                    builder.AppendLine(exception.Message);
                    foreach (var inner in inners)
                    {
                        builder.Append("  ");
                        builder.AppendLine(DisplayMessage(inner));
                    }

                    return builder.ToString();
            }
        }

        public static string DisplayMessage(TargetInvocationException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (exception.InnerException != null)
            {
                return DisplayMessage(exception.InnerException);
            }
            else
            {
                return exception.Message;
            }
        }
    }
}
