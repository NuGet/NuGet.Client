// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Resx = NuGet.PackageManagement.UI.Resources;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class OwnerAuthorViewModel : ViewModelBase
    {
        public OwnerAuthorViewModel(IReadOnlyList<string> trustedOwners)
        {
            if (trustedOwners != null && trustedOwners.Count > 0)
            {
                TrustedOwnerViewModels = new ObservableCollection<TrustedOwnerViewModel>(trustedOwners
                    .Select(owner => new TrustedOwnerViewModel(owner)).ToList());
            }
        }

        public ObservableCollection<TrustedOwnerViewModel> TrustedOwnerViewModels { get; }

        public string Owner { get; }

        public string Author { get; }

        public string ByOwner => !string.IsNullOrWhiteSpace(Owner) ? string.Format(CultureInfo.CurrentCulture, Resx.Text_ByOwner, Owner) : null;

        public string ByAuthor => !string.IsNullOrWhiteSpace(Author) ? string.Format(CultureInfo.CurrentCulture, Resx.Text_ByAuthor, Author) : null;

        public string ByOwnerOrAuthor => ByOwner ?? ByAuthor;

        public long? DownloadCount { get; set; }
    }
}
