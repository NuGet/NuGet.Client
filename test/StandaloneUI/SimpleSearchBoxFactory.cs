// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Controls;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace StandaloneUI
{
    public class SimpleSearchBoxFactory : IVsWindowSearchHostFactory
    {
        public IVsWindowSearchHost CreateWindowSearchHost(object pParentControl, IDropTarget pDropTarget = null)
        {
            var parent = pParentControl as Border;

            var box = new SimpleSearchBox();

            if (parent != null)
            {
                parent.Child = box;
            }

            return box;
        }
    }
}
