using System;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    public sealed class SimpleSearchBox : TextBox, IVsWindowSearchHost
    {
        private PackageManagerControl _control;

        public SimpleSearchBox()
        {
            this.KeyDown += SimpleSearchBox_KeyDown;
        }

        private void SimpleSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _control.Search(this.Text.Trim());
            }
        }

        public void SetupSearch(IVsWindowSearch pWindowSearch)
        {
            _control = pWindowSearch as PackageManagerControl;
        }

        public void Activate()
        {
            this.Focus();
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
            get { return new SimpleSearchQuery(this.Text); }
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
                throw new NotImplementedException();
            }

            public uint ParseError
            {
                get { throw new NotImplementedException(); }
            }

            public string SearchString
            {
                get;
                private set;
            }
        }
    }
}
