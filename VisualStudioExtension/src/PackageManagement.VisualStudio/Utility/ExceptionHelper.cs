// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class ExceptionHelper
    {
        public const string LogEntrySource = "NuGet Package Manager";

        public static void WriteToActivityLog(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            ActivityLog.LogError(LogEntrySource, exception.ToString());
        }

        public static Exception Unwrap(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            if (exception.InnerException == null)
            {
                return exception;
            }

            // Always return the inner exception from a target invocation exception
            if (exception is AggregateException
                ||
                exception is TargetInvocationException)
            {
                return exception.GetBaseException();
            }

            return exception;
        }
    }
}