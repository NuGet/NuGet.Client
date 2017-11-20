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

        public static SignatureLog InvalidInputError(string message)
        {
            return new SignatureLog(LogLevel.Error, NuGetLogCode.NU3001, message);
        }

        public static SignatureLog InvalidTimestampInSignatureError(string message)
        {
            return new SignatureLog(LogLevel.Error, NuGetLogCode.NU3022, message);
        }

        public static SignatureLog InvalidPackageError(string message)
        {
            return new SignatureLog(LogLevel.Error, NuGetLogCode.NU3002, message);
        }

        public static SignatureLog UntrustedRootWarning(string message)
        {
            return new SignatureLog(LogLevel.Warning, NuGetLogCode.NU3501, message);
        }

        public static SignatureLog SignatureInformationUnavailableWarning(string message)
        {
            return new SignatureLog(LogLevel.Warning, NuGetLogCode.NU3502, message);
        }

        public static SignatureLog TrustOfSignatureCannotBeProvenWarning(string message)
        {
            return new SignatureLog(LogLevel.Warning, NuGetLogCode.NU3502, message);
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
