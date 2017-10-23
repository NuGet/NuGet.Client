// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureIssue : IEquatable<SignatureIssue>
    {
        public bool Fatal { get; }

        public string Message { get; }

        public NuGetLogCode Code { get; }

        private SignatureIssue(bool fatal, NuGetLogCode code, string message)
        {
            Fatal = fatal;
            Code = code;
            Message = message;
        }

        public static SignatureIssue InvalidInputError(string message)
        {
            return new SignatureIssue(true, NuGetLogCode.NU3001, message);
        }

        public static SignatureIssue InvalidPackageError(string message)
        {
            return new SignatureIssue(true, NuGetLogCode.NU3002, message);
        }

        public static SignatureIssue UntrustedRootWarning(string message)
        {
            return new SignatureIssue(false, NuGetLogCode.NU3501, message);
        }

        public static SignatureIssue SignatureInformationUnavailableWarning(string message)
        {
            return new SignatureIssue(false, NuGetLogCode.NU3502, message);
        }

        public ILogMessage ToLogMessage()
        {
            if (Fatal)
            {
                return LogMessage.CreateError(Code, Message);
            }
            return LogMessage.CreateWarning(Code, Message);
        }

        public bool Equals(SignatureIssue other)
        {
            return other != null &&
                Equals(Fatal, other.Fatal) &&
                Equals(Code, other.Code) &&
                string.Equals(Message, other.Message, StringComparison.Ordinal);
        }
    }
}
