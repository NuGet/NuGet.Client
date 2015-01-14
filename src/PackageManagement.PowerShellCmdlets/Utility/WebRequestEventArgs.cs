using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class WebRequestEventArgs : EventArgs
    {
        public WebRequest Request { get; private set; }

        public WebRequestEventArgs(WebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            Request = request;
        }
    }
}
