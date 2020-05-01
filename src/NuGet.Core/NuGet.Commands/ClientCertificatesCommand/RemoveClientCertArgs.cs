// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using NuGet.Common;

namespace NuGet.Commands
{
    public sealed class RemoveClientCertArgs : BaseClientCertArgs
    {
        /// <summary>
        ///     Name of the package source.
        /// </summary>
        public string PackageSource { get; set; }

        public override void Validate()
        {
            if (!IsPackageSourceSettingProvided())
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                Strings.Error_PropertyCannotBeNullOrEmpty,
                                                                                nameof(PackageSource)));
            }
        }

        public bool IsPackageSourceSettingProvided()
        {
            return !string.IsNullOrEmpty(PackageSource);
        }
    }
}
