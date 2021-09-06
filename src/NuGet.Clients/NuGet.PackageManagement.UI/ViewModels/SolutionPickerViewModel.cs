// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.CodeContainerManagement;
using NuGet.VisualStudio;
using CodeContainer = Microsoft.VisualStudio.Shell.CodeContainerManagement.CodeContainer;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerViewModel
    {
        private int _maxResults = 10;

        public event EventHandler SolutionClicked;

        public SolutionPickerViewModel()
        {
            SolutionList = new ObservableCollection<SolutionPickerItemViewModel>();
            OpenSolutionCommand = new SolutionOpenCommand(this);
        }

        public async Task PopulateSolutionListAsync(CodeContainerStorageManagerFactory storageManagerFactory)
        {
            if (storageManagerFactory == null)
            {
                throw new ArgumentNullException(nameof(storageManagerFactory));
            }

            ICodeContainerStorageManager manager = await storageManagerFactory.CreateAsync();

            ActionBlock<StatefulReadOnlyList<CodeContainer, int?>> actionBlock = new ActionBlock<StatefulReadOnlyList<CodeContainer, int?>>(
                action: async codeContainerList =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        var list = codeContainerList.List;
                        await FilterListAsync(list);
                    });
                });
            await manager.SubscribeAsync(actionBlock, CancellationToken.None);
        }

        private async Task FilterListAsync(IReadOnlyList<CodeContainer> list)
        {
            List<CodeContainer> solutionList = list.OrderByDescending(cc => cc.LastAccessed).Take(_maxResults).ToList();
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (CodeContainer cc in solutionList)
            {
                SolutionList.Add(new SolutionPickerItemViewModel(OpenSolutionCommand, cc.LocalProperties.FullPath));
            }
        }

        private void RaiseSolutionClicked()
        {
            SolutionClicked?.Invoke(this, EventArgs.Empty);
        }

        public ObservableCollection<SolutionPickerItemViewModel> SolutionList { get; }

        public ICommand OpenSolutionCommand { get; }

        private class SolutionOpenCommand : ICommand
        {
            SolutionPickerViewModel _viewModel;

            public SolutionOpenCommand(SolutionPickerViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            // never raised
            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var filePath = parameter.ToString();
                if (File.Exists(filePath))
                {
                    var dte = Package.GetGlobalService(typeof(_DTE)) as DTE2;
                    if (dte != null)
                    {
                        dte.Solution.Open(filePath);
                        _viewModel.RaiseSolutionClicked();
                    }
                }
            }
        }
    }
}
