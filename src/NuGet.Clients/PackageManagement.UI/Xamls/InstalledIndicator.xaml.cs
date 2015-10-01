// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Controls;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    public partial class InstalledIndicator : UserControl
    {
        private static Brush _iconBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x32, 0x99, 0x32));

        public InstalledIndicator()
        {
            InitializeComponent();
            _icon.Brush = _iconBrush;
        }

        bool _isGrayed;

        public bool IsGrayed
        {
            get
            {
                return _isGrayed;
            }
            set
            {
                _isGrayed = value;
                if (_isGrayed)
                {
                    _icon.Brush = System.Windows.Media.Brushes.Gray;
                }
                else
                {
                    _icon.Brush = _iconBrush;
                }
            }
        }
    }
}
