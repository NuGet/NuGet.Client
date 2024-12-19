// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class ClearNuGetLocalsViewModel : ViewModelBase
    {
        private bool _isCommandComplete;
        private string _commandCompleteText;
        private Action _clearNuGetLocalsCommandExecute;
        private bool _isExecuting;

        private ClearNuGetLocalsViewModel()
        { }

        public ClearNuGetLocalsViewModel(Action clearNuGetLocalsCommandExecute)
        {
            _clearNuGetLocalsCommandExecute = clearNuGetLocalsCommandExecute ?? throw new ArgumentNullException(nameof(clearNuGetLocalsCommandExecute));
        }

        public void Execute()
        {
            if (_isExecuting)
            {
                return;
            }

            IsCommandComplete = false;
            _isExecuting = true;
            _clearNuGetLocalsCommandExecute();
            _isExecuting = false;
            CommandCompleteText = "Done clearing NuGet local resources at " + DateTime.Now.ToLongTimeString();
            IsCommandComplete = true;
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
