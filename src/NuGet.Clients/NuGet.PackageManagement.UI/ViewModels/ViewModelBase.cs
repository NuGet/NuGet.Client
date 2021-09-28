// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft;

namespace NuGet.PackageManagement.UI
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetValue<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!ReferenceEquals(field, value) && (value == null || !value.Equals(field)))
            {
                field = value;
                RaisePropertyChanged(propertyName);
                return true;
            }

            return false;
        }
    }
}
