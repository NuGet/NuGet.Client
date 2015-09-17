// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    public class InfiniteScrollListItemStyleSelector : StyleSelector
    {
        private Style PackageItemStyle { get; set; }
        private Style LoadingStatusIndicatorStyle { get; set; }

        private void Init(ItemsControl infiniteScrollList)
        {
            if (PackageItemStyle == null && LoadingStatusIndicatorStyle == null)
            {
                PackageItemStyle = (Style)infiniteScrollList.FindResource("packageItemStyle");
                LoadingStatusIndicatorStyle = (Style)infiniteScrollList.FindResource("loadingStatusIndicatorStyle");

                if (!StandaloneSwitch.IsRunningStandalone && PackageItemStyle.Setters.Count == 0)
                {
                    var setter = new Setter(InfiniteScrollList.TemplateProperty, infiniteScrollList.FindResource("ListBoxItemTemplate"));
                    PackageItemStyle.Setters.Add(setter);
                }
            }
        }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            Init(ItemsControl.ItemsControlFromItemContainer(container));

            if (item is LoadingStatusIndicator)
            {
                return LoadingStatusIndicatorStyle;
            }

            return PackageItemStyle;
        }
    }
}
