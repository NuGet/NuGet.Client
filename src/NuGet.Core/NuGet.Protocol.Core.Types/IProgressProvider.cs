using System;

namespace NuGet.Protocol.Core.Types
{
    public interface IProgressProvider
    {
        event EventHandler<PackageProgressEventArgs> ProgressAvailable;
    }
}