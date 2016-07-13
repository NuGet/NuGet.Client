using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets.Exceptions
{
    public class PackageSourceException : Exception
    {
        public enum ExceptionType {UnknownSource, UnknownSourceType};
        private ExceptionType Type;

        public PackageSourceException()
        {
        }

        public PackageSourceException(ExceptionType exceptionType)
        {
            Type = exceptionType;
        }

        public PackageSourceException(string message)
            : base(message)
        {
        }

        public PackageSourceException(string format, params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public PackageSourceException(string message, Exception exception)
            : base(message, exception)
        {
        }
    }
}
