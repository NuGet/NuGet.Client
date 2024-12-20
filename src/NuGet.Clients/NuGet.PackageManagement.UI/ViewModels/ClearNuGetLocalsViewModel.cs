// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
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

        internal void Execute()
        {
            if (_isExecuting)
            {
                return;
            }

            IsCommandComplete = false;
            CommandCompleteText = string.Empty;
            _isExecuting = true;

            var _ = ExecuteBackgroundWork()
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        OnCommandComplete(string.Format(CultureInfo.CurrentCulture, Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), task.Exception.InnerException.Message));
                    }
                    else
                    {
                        OnCommandComplete(message: string.Format(CultureInfo.CurrentCulture, Resources.ShowMessage_LocalsCommandSuccess, DateTime.Now.ToString(Resources.Culture)));
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async Task ExecuteBackgroundWork()
        {
            await Task.Run(async () =>
            {
                try
                {
                    await _clearNuGetLocalsCommandExecute();
                }
                catch (Exception ex)
                {
                    string failureMessage = string.Format(CultureInfo.CurrentCulture, Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message);
                    OnCommandComplete(failureMessage);
                    throw ex;
                }
            });
        }

        private void OnCommandComplete(string message)
        {
            _isExecuting = false;
            CommandCompleteText = message;
            IsCommandComplete = true;
        }
    }
}
