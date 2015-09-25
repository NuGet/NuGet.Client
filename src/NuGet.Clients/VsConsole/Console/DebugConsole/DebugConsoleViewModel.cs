// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NuGetConsole.DebugConsole
{
    public class DebugConsoleViewModel : INotifyPropertyChanged
    {
        private DebugConsoleLevel _activeLevel = DebugConsoleLevel.Trace;

        public DebugConsoleLevel ActiveLevel
        {
            get { return _activeLevel; }
            set { SetProperty(ref _activeLevel, value); }
        }

        public static IEnumerable<DebugConsoleLevel> AvailableLevels
        {
            get { return Enum.GetValues(typeof(DebugConsoleLevel)).Cast<DebugConsoleLevel>(); }
        }

        #region INotifyPropertyChanged

        // This section is ripe for plucking up into a base class!
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#", Justification = "A reference is desired here so that the backing field can be set by the helper method")]
        protected virtual void SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                RaisePropertyChanged(propertyName);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "It is an event.")]
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    public enum DebugConsoleLevel
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
}
