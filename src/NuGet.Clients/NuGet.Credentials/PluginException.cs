// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Credentials
{
    [Serializable]
    public class PluginException : Exception
    {
        public PluginException() { }

        public PluginException(string message) : base(message) { }

        public PluginException(string message, Exception inner) : base(message, inner) { }

        protected PluginException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }

        public static PluginException Create(string path, Exception inner)
        {
            return new PluginException(
                string.Format(Resources.PluginException_Exception_Format, path, inner.GetType().Name),
                inner);
        }

        public static PluginException CreateTimeoutMessage(string path, int timeoutMillis)
        {
            return new PluginException(
                string.Format(Resources.PluginException_Timeout_Format, path, timeoutMillis));
        }

        public static PluginException CreateWrappedExceptionMessage(
            string path, int exitCode, string stdout, string stderr)
        {
            var strings = new string[]
            {
                 string.Format(Resources.PluginException_Error_Format, path, exitCode),
                 stdout,
                 stderr
            }.Where(x => !string.IsNullOrWhiteSpace(x));

            return new PluginException(string.Join(Environment.NewLine, strings));
        }

        public static PluginException CreateNotStartedMessage(string path)
        {
            return new PluginException(string.Format(Resources.PluginException_NotStarted_Format, path));
        }

        public static PluginException CreatePathNotFoundMessage(string path, string attempted)
        {
            return new PluginException(string.Format(Resources.PluginException_PathNotFound_Format, path,
                attempted));
        }

        public static PluginException CreateAbortMessage(string path, string message)
        {
            return new PluginException(string.Format(Resources.PluginException_Abort_Format, path, message));
        }

        public static PluginException CreatePayloadExceptionMessage(
            string path,
            PluginCredentialResponseExitCode status,
            string payload)
        {
            return new PluginException(string.Format(
                Resources.PluginException_IncorrectPayload_Format,
                path,
                status,
                payload));
        }
    }
}
