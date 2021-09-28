// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    /// Adapted from MSDN implementation: https://msdn.microsoft.com/en-us/magazine/dd419663.aspx
    /// <summary>
    /// A command whose purpose is to relay its functionality to other objects by invoking delegates.
    /// The default value for CanExecute is true.
    /// </summary>
    internal class RelayCommand : ICommand
    {
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute)
            : this(execute, null)
        { }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        [DebuggerStepThrough]
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void Execute(object parameter) => _execute(parameter);
    }
}
