// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Commands
{
    public abstract class ModifyClientCertArgs : BaseClientCertArgs
    {
        /// <summary>
        /// FindBy added to a storage client certificate source
        /// </summary>
        public string FindBy { get; set; }

        /// <summary>
        ///     FindValue added to a storage client certificate source
        /// </summary>
        public string FindValue { get; set; }

        /// <summary>
        ///     Indicates that action forced
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        ///     Name of the package source.
        /// </summary>
        public string PackageSource { get; set; }

        /// <summary>
        ///     Password for the certificate, if needed. This option can be used to specify the password for the certificate.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     Path to certificate file added to a file client certificate source
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     StoreLocation added to a storage client certificate source
        /// </summary>
        public string StoreLocation { get; set; }

        /// <summary>
        ///     StoreName added to a storage client certificate source
        /// </summary>
        public string StoreName { get; set; }

        /// <summary>
        ///     Enables storing password for the certificate by disabling password encryption.
        /// </summary>
        public bool StorePasswordInClearText { get; set; }

        public override void Validate()
        {
            if (!IsPackageSourceSettingProvided())
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                Strings.Error_PropertyCannotBeNullOrEmpty,
                                                                                nameof(PackageSource)));
            }

            if (IsFileCertSettingsProvided() == IsStoreCertSettingsProvided())
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                Strings.Error_CouldNotUpdateClientCertificate,
                                                                                Strings.Error_InvalidCombinationOfArguments));
            }
        }

        public X509FindType? GetFindBy()
        {
            if (Enum.TryParse("FindBy" + FindBy, ignoreCase: true, result: out X509FindType value))
            {
                return value;
            }

            return null;
        }

        public StoreLocation? GetStoreLocation()
        {
            if (Enum.TryParse(StoreLocation, ignoreCase: true, result: out StoreLocation value))
            {
                return value;
            }

            return null;
        }

        public StoreName? GetStoreName()
        {
            if (Enum.TryParse(StoreName, ignoreCase: true, result: out StoreName value))
            {
                return value;
            }

            return null;
        }

        public bool IsFileCertSettingsProvided()
        {
            var isFilePathProvided = !string.IsNullOrEmpty(Path);
            var isPasswordProvided = !string.IsNullOrEmpty(Password);

            return isFilePathProvided || isPasswordProvided;
        }

        public bool IsPackageSourceSettingProvided()
        {
            return !string.IsNullOrEmpty(PackageSource);
        }

        public bool IsStoreCertSettingsProvided()
        {
            var isStoreLocationProvided = false;
            var isStoreNameProvided = false;
            var isFindTypeProvided = false;

            if (!string.IsNullOrWhiteSpace(StoreLocation))
            {
                var storeLocation = GetStoreLocation();
                if (storeLocation == null)
                {
                    throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                    Strings.Error_UnknownClientCertificatesStoreLocation,
                                                                                    StoreLocation));
                }

                isStoreLocationProvided = true;
            }

            if (!string.IsNullOrWhiteSpace(StoreName))
            {
                var storeName = GetStoreName();
                if (storeName == null)
                {
                    throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                    Strings.Error_UnknownClientCertificatesStoreName,
                                                                                    StoreName));
                }

                isStoreNameProvided = true;
            }

            if (!string.IsNullOrWhiteSpace(FindBy))
            {
                var findBy = GetFindBy();
                if (findBy == null)
                {
                    throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture,
                                                                                    Strings.Error_UnknownClientCertificatesFindBy,
                                                                                    FindBy));
                }

                isFindTypeProvided = true;
            }

            var isFindValueProvided = !string.IsNullOrEmpty(FindValue);

            return isStoreLocationProvided || isStoreNameProvided || isFindTypeProvided || isFindValueProvided;
        }
    }
}
