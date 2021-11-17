// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NuGet.PackageManagement.UI
{
    public class ItemsChangeObservableCollection<T> : ObservableCollection<T>
    {
        public void Refresh()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
