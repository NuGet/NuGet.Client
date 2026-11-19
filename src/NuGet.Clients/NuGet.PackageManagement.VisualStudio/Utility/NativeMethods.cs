// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace NuGet.PackageManagement.VisualStudio
{
    internal static class NativeMethods
    {
        public const int IDCANCEL = 2;
        public const int IDYES = 6;
        public const int IDNO = 7;

        private const int ErrorCancelled = 1223;
        private const int ErrorNotFound = 1168;
        private const int MaxPasswordLength = 256;
        private const int MaxUserNameLength = 513;
        private const uint CredentialTypeGeneric = 1;
        private const uint CredentialTypeDomainPassword = 2;
        private const uint CredentialPersistLocalMachine = 2;
        private const string PackageSourceCredentialComment = "NuGet package source credential";

        [Flags]
        private enum CredUiFlags
        {
            IncorrectPassword = 0x1,
            DoNotPersist = 0x2,
            ShowSaveCheckBox = 0x40,
            AlwaysShowUi = 0x80,
            GenericCredentials = 0x40000,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CredUiInfo
        {
            public int Size;
            public IntPtr ParentWindow;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string MessageText;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string CaptionText;

            public IntPtr Banner;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint Flags;
            public uint Type;
            public string? TargetName;
            public string? Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public string? UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref Credential credential, uint flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr buffer);

        internal static NetworkCredential? ReadCredentials(string targetName)
        {
            return ReadCredentials(targetName, ReadCredentials);
        }

        internal static NetworkCredential? ReadCredentials(
            string targetName,
            Func<string, uint, NetworkCredential?> readCredentials)
        {
            NetworkCredential? genericCredentials = readCredentials(targetName, CredentialTypeGeneric);
            if (genericCredentials != null)
            {
                return genericCredentials;
            }

            NetworkCredential? domainCredentials = readCredentials(targetName, CredentialTypeDomainPassword);
            if (domainCredentials != null)
            {
                using SecureString password = domainCredentials.SecurePassword;
                if (password.Length != 0)
                {
                    return domainCredentials;
                }
            }

            // VS stores a generic fallback when the domain password cannot be read.
            NetworkCredential? fallbackCredentials = readCredentials("VSCredentials_" + targetName, CredentialTypeGeneric);
            if (domainCredentials == null || (fallbackCredentials != null
                && string.Equals(domainCredentials.UserName, fallbackCredentials.UserName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(domainCredentials.Domain, fallbackCredentials.Domain, StringComparison.OrdinalIgnoreCase)))
            {
                return fallbackCredentials;
            }

            return null;
        }

        private static NetworkCredential? ReadCredentials(string targetName, uint type)
        {
            if (!CredRead(targetName, type, flags: 0, out IntPtr buffer))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                {
                    return null;
                }

                throw new Win32Exception(error);
            }

            try
            {
                Credential credential = Marshal.PtrToStructure<Credential>(buffer);
                if (credential.UserName == null)
                {
                    return null;
                }

                using var password = new SecureString();
                for (int index = 0; index < credential.CredentialBlobSize / sizeof(char); index++)
                {
                    password.AppendChar((char)Marshal.ReadInt16(credential.CredentialBlob, index * sizeof(char)));
                }

                password.MakeReadOnly();
                return CreateStoredCredentials(
                    credential.UserName,
                    password,
                    targetName,
                    credential.Comment == PackageSourceCredentialComment);
            }
            finally
            {
                CredFree(buffer);
            }
        }

        internal static NetworkCredential CreateStoredCredentials(
            string userName, SecureString password, string targetName, bool isPackageSourceCredential)
        {
            // The old VS prompt could prepend the target; the new prompt preserves the user's input.
            if (isPackageSourceCredential)
            {
                return new NetworkCredential(userName, password);
            }

            int separator = userName.IndexOf('\\');
            if (separator < 0)
            {
                return new NetworkCredential(userName, password);
            }

            string domain = userName.Substring(0, separator);
            userName = userName.Substring(separator + 1);
            if (string.Equals(domain, targetName, StringComparison.OrdinalIgnoreCase))
            {
                domain = string.Empty;
            }

            return new NetworkCredential(userName, password, domain);
        }

        internal static void WriteCredentials(string targetName, NetworkCredential credentials)
        {
            using SecureString password = credentials.SecurePassword;
            IntPtr buffer = Marshal.SecureStringToCoTaskMemUnicode(password);
            try
            {
                var credential = new Credential
                {
                    Type = CredentialTypeGeneric,
                    TargetName = targetName,
                    Comment = PackageSourceCredentialComment,
                    UserName = credentials.UserName,
                    CredentialBlob = buffer,
                    CredentialBlobSize = checked((uint)(password.Length * sizeof(char))),
                    Persist = CredentialPersistLocalMachine,
                };
                if (!CredWrite(ref credential, flags: 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(buffer);
            }
        }

        internal delegate NetworkCredential? CredentialPrompt(
            string caption,
            string message,
            string targetName,
            bool isRetry,
            IntPtr parentWindow,
            out bool saveCredentials);

        [DllImport("credui.dll", EntryPoint = "CredUIPromptForCredentialsW", CharSet = CharSet.Unicode)]
        private static extern int CredUIPromptForCredentials(
            ref CredUiInfo info,
            string targetName,
            IntPtr reserved,
            int authenticationError,
            StringBuilder userName,
            int maxUserNameCharacters,
            StringBuilder password,
            int maxPasswordCharacters,
            ref int save,
            CredUiFlags flags);

        internal static NetworkCredential? PromptForCredentials(
            string caption,
            string message,
            string targetName,
            bool isRetry,
            IntPtr parentWindow,
            out bool saveCredentials)
        {
            saveCredentials = false;
            var info = new CredUiInfo
            {
                Size = Marshal.SizeOf<CredUiInfo>(),
                ParentWindow = parentWindow,
                MessageText = message,
                CaptionText = caption,
            };
            var userName = new StringBuilder(MaxUserNameLength);
            var password = new StringBuilder(MaxPasswordLength);
            var save = 0;
            var flags = CredUiFlags.AlwaysShowUi | CredUiFlags.DoNotPersist |
                CredUiFlags.GenericCredentials | CredUiFlags.ShowSaveCheckBox;

            if (isRetry)
            {
                flags |= CredUiFlags.IncorrectPassword;
            }

            try
            {
                int result = CredUIPromptForCredentials(
                    ref info,
                    targetName,
                    IntPtr.Zero,
                    authenticationError: 0,
                    userName,
                    MaxUserNameLength,
                    password,
                    MaxPasswordLength,
                    ref save,
                    flags);

                if (result == ErrorCancelled)
                {
                    return null;
                }

                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                saveCredentials = save != 0;
                using (var securePassword = new SecureString())
                {
                    for (var index = 0; index < password.Length; index++)
                    {
                        securePassword.AppendChar(password[index]);
                    }

                    securePassword.MakeReadOnly();
                    return new NetworkCredential(userName.ToString(), securePassword);
                }
            }
            finally
            {
                for (var index = 0; index < password.Length; index++)
                {
                    password[index] = '\0';
                }
            }
        }
    }
}
