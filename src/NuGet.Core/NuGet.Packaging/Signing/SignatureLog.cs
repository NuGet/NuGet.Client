// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureLog : IEquatable<SignatureLog>
    {
        public LogLevel Level { get; }

        public string Message { get; }

        public NuGetLogCode Code { get; }

        private SignatureLog(LogLevel level, NuGetLogCode code, string message)
        {
            Level = level;
            Code = code;
            Message = message;
        }

        public static SignatureLog InformationLog(string message)
        {
            // create a log message and make the code undefined to not display the code in any logger
            return new SignatureLog(LogLevel.Information, NuGetLogCode.Undefined, message);
        }

        public static SignatureLog DetailedLog(string message)
        {
            // create a log message and make the code undefined to not display the code in any logger
            return new SignatureLog(LogLevel.Verbose, NuGetLogCode.Undefined, message);
        }

        public static SignatureLog DebugLog(string message)
        {
            return new SignatureLog(LogLevel.Debug, NuGetLogCode.Undefined, message);
        }

        public static SignatureLog Issue(bool fatal, NuGetLogCode code, string message)
        {
            // A fatal issue should be an error, otherwise just a warning
            var level = fatal ? LogLevel.Error : LogLevel.Warning;

            return new SignatureLog(level, code, message);
        }

        public static SignatureLog Error(NuGetLogCode code, string message)
        {
            return new SignatureLog(LogLevel.Error, code, message);
        }

        public ILogMessage ToLogMessage()
        {
            if (Level == LogLevel.Error)
            {
                return LogMessage.CreateError(Code, Message);
            }
            else if (Level == LogLevel.Warning)
            {
                return LogMessage.CreateWarning(Code, Message);
            }
            else
            {
                return new LogMessage(Level, Message);
            }
        }

        public bool Equals(SignatureLog other)
        {
            return other != null &&
                Equals(Level, other.Level) &&
                Equals(Code, other.Code) &&
                string.Equals(Message, other.Message, StringComparison.Ordinal);
        }
    }
}