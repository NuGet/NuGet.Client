using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public sealed class NuGetPackageManagerControlSearchTask : IVsSearchTask
    {
        private PackageManagerControl _packageManagerControl;
        private IVsSearchCallback _searchCallback;
        private IVsSearchQuery _searchQuery;

        public NuGetPackageManagerControlSearchTask(PackageManagerControl packageManagerControl, uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            _packageManagerControl = packageManagerControl;
            _searchCallback = pSearchCallback;
            _searchQuery = pSearchQuery;
            Id = dwCookie;
            ErrorCode = 0;
            SetStatus(VsSearchTaskStatus.Created);

        }
        public void Start()
        {
            SetStatus(VsSearchTaskStatus.Started);
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _packageManagerControl.SearchPackagesAndRefreshUpdateCount(_searchQuery.SearchString, true, _searchCallback, this);
                SetStatus(VsSearchTaskStatus.Completed);
            });
        }

        public uint Id { get; private set; }

        public IVsSearchQuery SearchQuery
        {
            get
            {
                return _searchQuery;
            }
            set
            {
                _searchQuery = value;
            }
        }

        public uint Status { get; private set; }

        public int ErrorCode { get; private set; }

        public void Stop()
        {
            SetStatus(VsSearchTaskStatus.Stopped);
        }

        private void SetStatus(VsSearchTaskStatus taskStatus)
        {
            Status = (uint)taskStatus;
        }

        private enum VsSearchTaskStatus : uint
        {
            Completed = 2,
            Created = 0,
            Error = 4,
            Started = 1,
            Stopped = 3
        }
    }
}
