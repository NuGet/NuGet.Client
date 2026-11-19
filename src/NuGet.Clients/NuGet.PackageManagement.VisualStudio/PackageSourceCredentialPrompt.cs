// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Prompts for source credentials, optionally saving them in Windows Credential Manager.
    /// Saved credentials use NuGet-prefixed scheme-and-server targets, so they are isolated
    /// from other applications and from sources with a different scheme or port.
    /// </summary>
    internal sealed class PackageSourceCredentialPrompt : IPackageSourceCredentialPrompt
    {
        private readonly Func<string, NetworkCredential?> _readCredentials;
        private readonly NativeMethods.CredentialPrompt _promptForCredentials;
        private readonly Action<string, NetworkCredential> _writeCredentials;

        public PackageSourceCredentialPrompt()
            : this(NativeMethods.ReadCredentials, NativeMethods.PromptForCredentials, NativeMethods.WriteCredentials)
        {
        }

        internal PackageSourceCredentialPrompt(
            Func<string, NetworkCredential?> readCredentials,
            NativeMethods.CredentialPrompt promptForCredentials,
            Action<string, NetworkCredential> writeCredentials)
        {
            _readCredentials = readCredentials;
            _promptForCredentials = promptForCredentials;
            _writeCredentials = writeCredentials;
        }

        public NetworkCredential? PromptForCredentials(Uri packageSourceUri, bool isRetry, IntPtr parentWindow)
        {
            string targetName = "NuGet:" + packageSourceUri.GetComponents(
                UriComponents.SchemeAndServer,
                UriFormat.UriEscaped);
            if (!isRetry && _readCredentials(targetName) is NetworkCredential savedCredentials)
            {
                return savedCredentials;
            }

            string source = GetDisplayName(packageSourceUri);
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Strings.CredentialPrompt_PackageSourceMessage,
                source);

            NetworkCredential? credentials = _promptForCredentials(
                Strings.CredentialPrompt_PackageSourceCaption,
                message,
                targetName,
                isRetry,
                parentWindow,
                out bool saveCredentials);

            if (credentials != null && saveCredentials)
            {
                _writeCredentials(targetName, credentials);
            }

            return credentials;
        }

        internal static string GetDisplayName(Uri packageSourceUri)
        {
            return packageSourceUri.GetComponents(
                UriComponents.SchemeAndServer | UriComponents.Path,
                UriFormat.SafeUnescaped);
        }
    }
}
