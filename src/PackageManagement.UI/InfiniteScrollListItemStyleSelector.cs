// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    public class InfiniteScrollListItemStyleSelector : StyleSelector
    {
        private static InfiniteScrollList InfiniteScrollList = new InfiniteScrollList();

        private Style PackageItemStyle { get; set; }
        private Style LoadingStatusIndicatorStyle { get; set; }

        private void Init()
        {
            if (InfiniteScrollList != null && PackageItemStyle == null && LoadingStatusIndicatorStyle == null)
            {
                PackageItemStyle = (Style)InfiniteScrollList.FindResource("packageItemStyle");
                LoadingStatusIndicatorStyle = (Style)InfiniteScrollList.FindResource("loadingStatusIndicatorStyle");

                if (!StandaloneSwitch.IsRunningStandalone && PackageItemStyle.Setters.Count == 0)
                {
                    var setter = new Setter(InfiniteScrollList.TemplateProperty, InfiniteScrollList.FindResource("ListBoxItemTemplate"));
                    PackageItemStyle.Setters.Add(setter);
                }
            }
        }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            Init();

            if (item is LoadingStatusIndicator)
            {
                return LoadingStatusIndicatorStyle;
            }

            return PackageItemStyle;
        }
    }
}
