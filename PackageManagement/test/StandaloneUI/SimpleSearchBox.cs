// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using NuGet.PackageManagement.UI;

namespace StandaloneUI
{
    public sealed class SimpleSearchBox : TextBox, IVsWindowSearchHost
    {
        private PackageManagerControl _control;

        public SimpleSearchBox()
        {
            KeyDown += SimpleSearchBox_KeyDown;
        }

        private void SimpleSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _control.Search(Text.Trim());
            }
        }

        public void SetupSearch(IVsWindowSearch pWindowSearch)
        {
            _control = pWindowSearch as PackageManagerControl;
        }

        public void Activate()
        {
            Focus();
        }

        public string HelpTopic { get; set; }

        bool IVsWindowSearchHost.IsEnabled { get; set; }

        public bool IsPopupVisible { get; set; }

        bool IVsWindowSearchHost.IsVisible { get; set; }

        public void SearchAsync(IVsSearchQuery pSearchQuery)
        {
            _control.CreateSearch(0, pSearchQuery, null);
        }

        public IVsWindowSearchEvents SearchEvents
        {
            get { return null; }
        }

        public IVsWindowSearch SearchObject
        {
            get { return null; }
        }

        public IVsSearchQuery SearchQuery
        {
            get { return new SimpleSearchQuery(Text); }
        }

        public IVsSearchQueryParser SearchQueryParser
        {
            get { return null; }
        }

        public IVsSearchTask SearchTask
        {
            get { return null; }
        }

        public void TerminateSearch()
        {
        }

        internal class SimpleSearchQuery : IVsSearchQuery
        {
            public SimpleSearchQuery(string searchString)
            {
                SearchString = searchString;
            }

            public uint GetTokens(uint dwMaxTokens, IVsSearchToken[] rgpSearchTokens)
            {
                // no op
                Debug.Assert(false, "Not Implemented");
                return 0;
            }

            public uint ParseError
            {
                get
                { 
                    // no op
                    Debug.Assert(false, "Not Implemented");
                    return 0;
                }
            }

            public string SearchString { get; }
        }
    }
}
