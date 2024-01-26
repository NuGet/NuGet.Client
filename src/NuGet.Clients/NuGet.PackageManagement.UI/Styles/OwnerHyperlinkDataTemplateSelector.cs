// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI.Styles
{
    public class OwnerHyperlinkDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate BeforeLastTemplate { get; set; }

        public DataTemplate LastTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            //ItemsControl itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
            //itemsControl.ItemContainerGenerator.container)

            if (item != null && container != null)
            {
                DependencyObject dependencyObject = item as DependencyObject;
                ItemsControl itemsControl = ItemsControl.ItemsControlFromItemContainer(container);

                if (itemsControl != null
                    && itemsControl.Items.Count > 0
                    && dependencyObject != null)
                {
                    if (itemsControl.Items.Count == 1)
                    {
                        return LastTemplate;
                    }

                    int last = itemsControl.Items.Count - 1;
                    int index = itemsControl.ItemContainerGenerator.IndexFromContainer(dependencyObject, returnLocalIndex: true);

                    if (index == last)
                    {
                        return LastTemplate;
                    }

                    return BeforeLastTemplate;
                }
            }

            return base.SelectTemplate(item, container);
        }
    }
}
