// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NuGet.Packaging.Signing
{
    internal static class NativeMethods
    {
        // http://msdn.microsoft.com/en-us/library/windows/desktop/aa380228(v=vs.85).aspx
        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern SafeCryptMsgHandle CryptMsgOpenToDecode(
            CMSG_ENCODING dwMsgEncodingType,
            CMSG_OPENTODECODE_FLAGS dwFlags,
            uint dwMsgType,
            IntPtr hCryptProv,
            IntPtr pRecipientInfo,
            IntPtr pStreamInfo);

        // http://msdn.microsoft.com/en-us/library/windows/desktop/aa380219(v=vs.85).aspx
        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern bool CryptMsgClose(IntPtr hCryptMsg);

        // http://msdn.microsoft.com/en-us/library/windows/desktop/aa380231(v=vs.85).aspx
        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern bool CryptMsgUpdate(
            SafeCryptMsgHandle hCryptMsg,
            byte[] pbData,
            uint cbData,
            bool fFinal);
    }

    internal sealed class SafeCryptMsgHandle : SafeHandle
    { 
        internal static SafeCryptMsgHandle InvalidHandle => new SafeCryptMsgHandle(IntPtr.Zero);

        public override bool IsInvalid => handle == IntPtr.Zero;

        private SafeCryptMsgHandle()
            : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        internal SafeCryptMsgHandle(IntPtr handle)
            : base(handle, ownsHandle: true)
        {
        }

        internal SafeCryptMsgHandle(IntPtr handle, bool ownsHandle)
            : base(handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CryptMsgClose(handle);
        }
    }

    [Flags]
    internal enum CMSG_OPENTODECODE_FLAGS : uint
    {
        // Source: wincrypt.h
        None = 0,
        CMSG_DETACHED_FLAG = 0x00000004,
        CMSG_CRYPT_RELEASE_CONTEXT_FLAG = 0x00008000
    }

    [Flags]
    internal enum CMSG_ENCODING : uint
    {
        // Source: wincrypt.h
        X509_ASN_ENCODING = 0x00000001,
        PKCS_7_NDR_ENCODING = 0x00010000,
        Any = X509_ASN_ENCODING | PKCS_7_NDR_ENCODING
    }
}
