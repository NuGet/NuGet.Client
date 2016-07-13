// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class PackageSourceException : Exception
    {
        public enum ExceptionType { Default, UnknownSource, UnknownSourceType };
        private ExceptionType type;

        public ExceptionType Type
        {
            get
            {
                return type;
            }
        }

        public PackageSourceException()
        {
            type = ExceptionType.Default;
        }

        public PackageSourceException(ExceptionType exceptionType)
        {
            type = exceptionType;
        }

        public PackageSourceException(string message)
            : base(message)
        {
            type = ExceptionType.Default;
        }
    }
}
