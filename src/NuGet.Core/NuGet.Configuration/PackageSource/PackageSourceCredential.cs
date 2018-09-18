// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Shared;

namespace NuGet.Configuration
{
    /// <summary>
    /// Represents credentials required to authenticate user within package source web requests.
    /// </summary>
    public class PackageSourceCredential : IEquatable<PackageSourceCredential>
    {
        /// <summary>
        /// User name
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Password text as stored in config file. May be encrypted.
        /// </summary>
        public string PasswordText { get; }

        /// <summary>
        /// Indicates if password is stored in clear text.
        /// </summary>
        public bool IsPasswordClearText { get; }

        private readonly Lazy<int> _hashCode;

        /// <summary>
        /// Retrieves password in clear text. Decrypts on-demand.
        /// </summary>
        public string Password
        {
            get
            {
                if (PasswordText != null && !IsPasswordClearText)
                {
                    try
                    {
                        return EncryptionUtility.DecryptString(PasswordText);
                    }
                    catch (NotSupportedException e)
                    {
                        throw new NuGetConfigurationException(
                            string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedDecryptPassword, Source), e);
                    }
                }
                else
                {
                    return PasswordText;
                }
            }
        }

        /// <summary>
        /// Associated source ID
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Verifies if object contains valid data, e.g. not empty user name and password.
        /// </summary>
        /// <returns>True if credentials object is valid</returns>
        public bool IsValid() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(PasswordText);

        /// <summary>
        /// Instantiates the credential instance out of raw values read from a config file.
        /// </summary>
        /// <param name="source">Associated source ID (needed for reporting errors)</param>
        /// <param name="username">User name</param>
        /// <param name="passwordText">Password as stored in config file</param>
        /// <param name="isPasswordClearText">Hints if password provided in clear text</param>
        public PackageSourceCredential(string source, string username, string passwordText, bool isPasswordClearText)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            PasswordText = passwordText;
            IsPasswordClearText = isPasswordClearText;

            _hashCode = new Lazy<int>(() =>
            {
                var combiner = new HashCodeCombiner();

                combiner.AddObject(Source);
                combiner.AddObject(Username);
                combiner.AddObject(PasswordText);
                combiner.AddObject(IsPasswordClearText);

                return combiner.GetHashCode();
            });
        }

        /// <summary>
        /// Creates new instance of credential object out values provided by user.
        /// </summary>
        /// <param name="source">Source ID needed for reporting errors if any</param>
        /// <param name="username">User name</param>
        /// <param name="password">Password text in clear</param>
        /// <param name="storePasswordInClearText">Hints if the password should be stored in clear text on disk.</param>
        /// <returns>New instance of <see cref="PackageSourceCredential"/></returns>
        public static PackageSourceCredential FromUserInput(string source, string username, string password, bool storePasswordInClearText)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (username == null)
            {
                throw new ArgumentNullException(nameof(username));
            }

            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            try
            {
                var passwordText = storePasswordInClearText ? password: EncryptionUtility.EncryptString(password);
                return new PackageSourceCredential(source, username, passwordText, isPasswordClearText: storePasswordInClearText);
            }
            catch (NotSupportedException e)
            {
                throw new NuGetConfigurationException(
                    string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedEncryptPassword, source), e);
            }
        }

        internal PackageSourceCredential Clone()
        {
            return new PackageSourceCredential(Source, Username, PasswordText, IsPasswordClearText);
        }


        public CredentialsItem AsCredentialsItem()
        {
            return new CredentialsItem(Source, Username, PasswordText, IsPasswordClearText);
        }

        public bool Equals(PackageSourceCredential other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                string.Equals(Username, other.Username, StringComparison.Ordinal) &&
                string.Equals(PasswordText, other.PasswordText, StringComparison.Ordinal) &&
                IsPasswordClearText == other.IsPasswordClearText;
        }

        public override bool Equals(object other)
        {
            return Equals(other as PackageSourceCredential);
        }

        public override int GetHashCode()
        {
            return _hashCode.Value;
        }
    }
}