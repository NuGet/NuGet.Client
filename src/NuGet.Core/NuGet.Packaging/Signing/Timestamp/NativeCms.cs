// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NuGet.Packaging.Signing
{
    internal class NativeCms : IDisposable
    {
        private SafeCryptMsgHandle _handle;
        private bool _detached;

        private NativeCms(SafeCryptMsgHandle handle, bool detached)
        {
            _handle = handle;
            _detached = detached;
        }

        internal byte[] GetEncryptedDigest()
        {
            return GetByteArrayAttribute(CMSG_GETPARAM_TYPE.CMSG_ENCRYPTED_DIGEST, index: 0);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }

        private byte[] GetByteArrayAttribute(CMSG_GETPARAM_TYPE param, uint index)
        {
            uint valueLength = 0;

            NativeUtilities.ThrowIfFailed(NativeMethods.CryptMsgGetParam(
                _handle,
                param,
                index,
                null,
                ref valueLength));

            var data = new byte[(int)valueLength];

            NativeUtilities.ThrowIfFailed(NativeMethods.CryptMsgGetParam(
                _handle,
                param,
                index,
                data,
                ref valueLength));

            return data;
        }

        internal static NativeCms Decode(byte[] input, bool detached)
        {
            var handle = NativeMethods.CryptMsgOpenToDecode(
                CMSG_ENCODING.Any,
                detached ? CMSG_OPENTODECODE_FLAGS.CMSG_DETACHED_FLAG : CMSG_OPENTODECODE_FLAGS.None,
                0u,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            // Load the data into the message
            if (!NativeMethods.CryptMsgUpdate(handle, input, (uint)input.Length, fFinal: true))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            return new NativeCms(handle, detached);
        }
    }
}
