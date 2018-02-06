using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClient.Test.Foundation.Utility
{
    public static class ErrorHandling
    {
        // IMPORTANT: DOCUMENT FULLY

        // Be careful with what is caught here.  We should be attempting to catch issues that are outside of our control (access issues, build engine failures,
        // xml parser errors), not coding errors such as null ref, etc.

        private static List<Func<Exception, bool>> exceptionHandlers = new List<Func<Exception, bool>>();

        static ErrorHandling()
        {
            ErrorHandling.exceptionHandlers.Add(ErrorHandling.BasicIOExceptionHandler);
            ErrorHandling.exceptionHandlers.Add(ErrorHandling.BasicXmlExceptionHandler);
        }

        /// <summary>
        /// Returns true if the given exception is a "normal" exception from instantiating types
        /// (the type or assembly couldn't be found, accessed, etc.).
        /// </summary>
        public static bool TypeLoadExceptionHandler(Exception exception)
        {
            if ((exception is BadImageFormatException)
                || (exception is MissingMemberException)
                || (exception is SecurityException)
                || (exception is IOException)
                || (exception is TargetInvocationException)
                || (exception is TypeInitializationException))
            {
                // Expected from type loading issues
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the given exception is a "normal" exception received from IO operations
        /// (the file couldn't be found, accessed, etc.).
        /// </summary>
        public static bool BasicIOExceptionHandler(Exception exception)
        {
            // ArgumentException is thrown by multiple framework apis when there are illegal characters in a path.
            // They throw in the method System.IO.Path.CheckInvalidPathChars.  We'll look for this specifically as
            // catching this exception is generally really bad news.  Ideally all code should check path validity
            // before performing operations.
            if (exception.GetType() == typeof(ArgumentException))
            {
                if (exception.StackTrace.Contains("System.IO.Path.CheckInvalidPathChars")
                    || exception.StackTrace.Contains("System.IO.Path.NormalizePath"))  // PS 102763
                {
                    Debug.Fail("Should validate paths before performing operations on them.");
                    return true;
                }
                else
                {
                    return false;
                }
            }

            if ((exception is System.IO.IOException) // Change 32814
                                                     // InvalidDataException does not derive from IOException. Added to fix bug 323931
                || (exception is System.IO.InvalidDataException)
                // SecurityException and NotSupportedException were introduced by change 53397.  There is no
                // description of where these are thrown from in the changelist or the associated bug.
                || (exception is System.Security.SecurityException)
                || (exception is UnauthorizedAccessException) // Change 32814
                )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the exception is related to bad/unparseable XML.
        /// </summary>
        public static bool BasicXmlExceptionHandler(Exception exception)
        {
            return exception is System.Xml.XmlException; // Change 75673
        }

        /// <summary>
        /// Determines if the exception caught and handled internally or if it should be re-thrown.
        /// </summary>
        /// <remarks>Avoid catching silently.  Try to limit the scope of try catch blocks.</remarks>
        /// <param name="exception">The exception just caught.</param>
        /// <param name="exceptionHandlers">List of handlers to use.</param>
        public static bool ShouldHandleExceptions(Exception exception, params Func<Exception, bool>[] exceptionHandlers)
        {
            return ErrorHandling.ShouldHandleExceptions(exception, exceptionHandlers.AsEnumerable());
        }

        /// <summary>
        /// Determines if the exception caught and handled internally or if it should be re-thrown.
        /// </summary>
        /// <remarks>Avoid catching silently.  Try to limit the scope of try catch blocks.</remarks>
        /// <param name="exception">The exception just caught.</param>
        /// <param name="exceptionHandlers">List of handlers to use.</param>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "Using standard funcs.")]
        public static bool ShouldHandleExceptions(Exception exception, IEnumerable<Func<Exception, bool>> exceptionHandlers)
        {
            if (exceptionHandlers.Any(shouldHandleExceptions => shouldHandleExceptions(exception)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if the exception caught and handled internally or if it should be re-thrown.
        /// </summary>
        /// <remarks>Avoid catching silently.  Try to limit the scope of try catch blocks.</remarks>
        /// <param name="exception">The exception just caught.</param>
        public static bool ShouldHandleExceptions(Exception exception)
        {
            return ErrorHandling.ShouldHandleExceptions(exception, ErrorHandling.exceptionHandlers);
        }

        /// <summary>
        /// Call this method to wrap an action with basic exception handling.
        /// </summary>
        /// <param name="handledExceptionAction">Optional action to invoke with handled exceptions.</param>
        /// <returns>
        /// 'true' if no exceptions were swallowed.
        /// </returns>
        public static bool HandleBasicExceptions(Action action, Action<Exception> handledExceptionAction)
        {
            return ErrorHandling.HandleBasicExceptions(action, handledExceptionAction, null);
        }

        /// <summary>
        /// Call this method to wrap an action with basic exception handling.
        /// </summary>
        /// <param name="handledExceptionAction">Optional action to invoke with handled exceptions.</param>
        /// <returns>
        /// 'true' if no exceptions were swallowed.
        /// </returns>
        public static bool HandleBasicExceptions(Action action, Action<Exception> handledExceptionAction, params Func<Exception, bool>[] exceptionHandlers)
        {
            try
            {
                action.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                if ((exceptionHandlers != null && ErrorHandling.ShouldHandleExceptions(exception, exceptionHandlers))
                    || ErrorHandling.ShouldHandleExceptions(exception))
                {
                    if (handledExceptionAction != null)
                    {
                        handledExceptionAction(exception);
                    }
                    return false;
                }
                throw;
            }
        }

        /// <summary>
        /// Call this method to wrap an action with basic exception handling.
        /// </summary>
        /// <param name="exceptionHandlers">List of handlers to use.</param>
        /// <returns>'true' if no exceptions were swallowed.</returns>
        public static bool HandleBasicExceptions(Action action, params Func<Exception, bool>[] exceptionHandlers)
        {
            return ErrorHandling.HandleBasicExceptions(action: action, handledExceptionAction: null, exceptionHandlers: exceptionHandlers);
        }

        /// <summary>
        /// Call this method to wrap an action with basic exception handling.
        /// </summary>
        /// <returns>
        /// 'true' if no exceptions were swallowed.
        /// </returns>
        public static bool HandleBasicExceptions(Action action)
        {
            return ErrorHandling.HandleBasicExceptions(action: action, handledExceptionAction: null);
        }

        /// <summary>
        /// Call this method to wrap an action with type load exception handling.
        /// </summary>
        /// <returns>
        /// 'true' if no exceptions were swallowed.
        /// </returns>
        public static bool HandleTypeLoadExceptions(Action action)
        {
            return ErrorHandling.HandleBasicExceptions(action: action, handledExceptionAction: null, exceptionHandlers: TypeLoadExceptionHandler);
        }
    }
}
