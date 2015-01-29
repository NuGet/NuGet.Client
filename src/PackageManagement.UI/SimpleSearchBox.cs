using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

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

        public string HelpTopic
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        bool IVsWindowSearchHost.IsEnabled
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool IsPopupVisible
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        private bool _visible;
        public bool IsVisible
        {
            get
            {
                return _visible;
            }
            set
            {
                _visible = value;
            }
        }

        public void SearchAsync(IVsSearchQuery pSearchQuery)
        {
            _control.CreateSearch(0, pSearchQuery, null);
        }

        public IVsWindowSearchEvents SearchEvents
        {
            get { throw new NotImplementedException(); }
        }

        public IVsWindowSearch SearchObject
        {
            get { throw new NotImplementedException(); }
        }

        public IVsSearchQuery SearchQuery
        {
            get { return new SimpleSearchQuery(this.Text); }
        }

        public IVsSearchQueryParser SearchQueryParser
        {
            get { throw new NotImplementedException(); }
        }

        public IVsSearchTask SearchTask
        {
            get { throw new NotImplementedException(); }
        }

        public void TerminateSearch()
        {
            throw new NotImplementedException();
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
