using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public class PackagingException : Exception
    {
        public PackagingException(string message)
            : base(message)
        {

        }
    }
}
