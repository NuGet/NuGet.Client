// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    public class InfiniteScrollListItemStyleSelector : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container)
        {
            if (item is LoadingStatusIndicator)
            {
                return InfiniteScrollList.LoadingStatusIndicatorStyle;
            }

            return InfiniteScrollList.PackageItemStyle;
        }
    }
}
