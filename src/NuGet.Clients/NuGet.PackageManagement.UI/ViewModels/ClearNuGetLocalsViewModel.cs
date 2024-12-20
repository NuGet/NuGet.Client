// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class ClearNuGetLocalsViewModel : ViewModelBase
    {
        private bool _isCommandComplete;
        private string _commandCompleteText;
        private Func<Task> _clearNuGetLocalsCommandExecute;
        private bool _isExecuting;

        private ClearNuGetLocalsViewModel()
        { }

        public ClearNuGetLocalsViewModel(Func<Task> clearNuGetLocalsCommandExecute)
        {
            _clearNuGetLocalsCommandExecute = clearNuGetLocalsCommandExecute ?? throw new ArgumentNullException(nameof(clearNuGetLocalsCommandExecute));
        }

        internal void Execute()
        {
            if (_isExecuting)
            {
                return;
            }

            IsCommandComplete = false;
            _isExecuting = true;
            var task1 = GetDataWithBackgroundWork().ContinueWith(t => OnCommandComplete(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnCommandComplete()
        {
            _isExecuting = false;
            CommandCompleteText = "Done clearing NuGet local resources at " + DateTime.Now.ToLongTimeString();
            IsCommandComplete = true;
        }

        private async Task GetDataWithBackgroundWork()
        {
            await Task.Run(async () =>
            {
                await _clearNuGetLocalsCommandExecute();
            });
        }

        public bool IsCommandComplete
        {
            get
            {
                return _isCommandComplete;
            }
            set
            {
                SetAndRaisePropertyChanged(ref _isCommandComplete, value);
            }
        }
        public string CommandCompleteText
        {
            get
            {
                return _commandCompleteText;
            }
            set
            {
                SetAndRaisePropertyChanged(ref _commandCompleteText, value);
            }
        }
    }
}
