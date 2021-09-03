// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.CodeContainerManagement;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks.Dataflow;
using CodeContainer = Microsoft.VisualStudio.Shell.CodeContainerManagement.CodeContainer;
using System.Threading;
using NuGet.VisualStudio;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    public class SolutionPickerViewModel
    {
        private ObservableCollection<SolutionPickerItemViewModel> _solutionPathList;
        private int _maxResults = 10;
        private ICommand _openSolutionCommand;

        public SolutionPickerViewModel()
        {
            _solutionPathList = new ObservableCollection<SolutionPickerItemViewModel>();
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
                var item = new SolutionPickerItemViewModel(OpenSolution, cc.LocalProperties.FullPath);
                _solutionPathList.Add(item);
            }
        }

        public ObservableCollection<SolutionPickerItemViewModel> SolutionList
        {
            get
            {
                return _solutionPathList;
            }
        }

        public ICommand OpenSolution
        {
            get
            {
                if (_openSolutionCommand == null)
                {
                    _openSolutionCommand = new URISolutionOpenerCommand();
                }
                return _openSolutionCommand;
            }
        }
    }
}
