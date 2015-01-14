using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public interface IHttpClientEvents : IProgressProvider
    {
        event EventHandler<WebRequestEventArgs> SendingRequest;
    }

    public interface IProgressProvider
    {
        event EventHandler<ProgressEventArgs> ProgressAvailable;
    }

    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(int percentComplete)
            : this(null, percentComplete)
        {
        }

        public ProgressEventArgs(string operation, int percentComplete)
        {
            Operation = operation;
            PercentComplete = percentComplete;
        }

        public string Operation { get; private set; }
        public int PercentComplete { get; private set; }
    }
}
