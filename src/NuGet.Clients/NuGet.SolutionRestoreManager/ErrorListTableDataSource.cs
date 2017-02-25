using System;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using NuGet.PackageManagement.UI;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Add/Remove warnings/errors from the error list.
    /// This persists messages once they are added.
    /// </summary>
    [Export(typeof(ErrorListTableDataSource))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ErrorListTableDataSource : ITableDataSource
    {
        private readonly object _lockObj = new object();
        private readonly IServiceProvider _serviceProvider;

        private TableSubscription _tableSubscription;

        public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

        public string Identifier => "NuGetRestoreManagerListTable";

        public string DisplayName => "NuGet_Restore_Manager_Table_Data_Source";

        public IErrorList _errorList;
        public ITableManager _tableManager;
        private bool _initialized;

        [ImportingConstructor]
        public ErrorListTableDataSource(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            lock (_lockObj)
            {
                _tableSubscription = new TableSubscription(sink, _lockObj);
                return _tableSubscription;
            }
        }

        /// <summary>
        /// Clear only nuget entries.
        /// </summary>
        public void ClearNuGetEntries()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnsureInitialized();

            lock (_lockObj)
            {
                var sink = _tableSubscription?.TableDataSink;

                if (sink != null)
                {
                    var entries = _errorList.TableControl?.Entries?.Where(IsNuGetEntry).ToArray()
                        ?? new ITableEntryHandle[0];

                    sink.RemoveEntries(entries);
                }
            }
        }

        /// <summary>
        /// Add error list entries
        /// </summary>
        public void AddEntries(params ErrorListTableEntry[] entries)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnsureInitialized();

            lock (_lockObj)
            {
                var sink = _tableSubscription?.TableDataSink;

                if (sink != null)
                {
                    sink.AddEntries(entries.ToList(), removeAllEntries: false);
                }
            }
        }

        /// <summary>
        /// Show error window.
        /// </summary>
        public void BringToFront()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnsureInitialized();

            // Give the error list focus.
            var vsErrorList = _errorList as IVsErrorList;
            vsErrorList?.BringToFront();
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _errorList = _serviceProvider.GetService(typeof(SVsErrorList)) as IErrorList;
                _tableManager = _errorList?.TableControl?.Manager;

                if (_tableManager != null)
                {
                    _initialized = true;
                    _tableManager.AddSource(this);
                }
            }
        }

        private static bool IsNuGetEntry(ITableEntryHandle entry)
        {
            object sourceObj;
            return (entry != null
                && entry.TryGetValue(StandardTableColumnDefinitions.ErrorSource, out sourceObj)
                && StringComparer.Ordinal.Equals(ErrorListTableEntry.ErrorSouce, (sourceObj as string)));
        }

        public void Dispose()
        {
            if (_tableManager != null && _initialized)
            {
                try
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        _tableManager.RemoveSource(this);
                    });
                }
                catch (ObjectDisposedException)
                {
                    // Ignore disposed exceptions
                }
            }
        }

        /// <summary>
        /// Holds an ITableDataSink and lock.
        /// </summary>
        private class TableSubscription : IDisposable
        {
            public ITableDataSink TableDataSink { get; private set; }

            private readonly object _lockObj;

            public TableSubscription(ITableDataSink sink, object lockObj)
            {
                TableDataSink = sink;
                _lockObj = lockObj;
            }

            public void Dispose()
            {
                lock (_lockObj)
                {
                    TableDataSink = null;
                }
            }
        }
    }
}
