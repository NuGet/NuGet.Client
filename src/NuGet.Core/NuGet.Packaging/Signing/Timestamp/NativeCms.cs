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

        internal void AddCertificates(IEnumerable<byte[]> encodedCertificates)
        {
            foreach (var cert in encodedCertificates)
            {
                // Construct the blob
                var unmanagedCert = IntPtr.Zero;
                var unmanagedBlob = IntPtr.Zero;
                try
                {
                    // Build blob holder
                    unmanagedCert = Marshal.AllocHGlobal(cert.Length);
                    Marshal.Copy(cert, 0, unmanagedCert, cert.Length);
                    var blob = new CRYPT_INTEGER_BLOB_INTPTR()
                    {
                        cbData = (uint)cert.Length,
                        pbData = unmanagedCert
                    };

                    // Copy it to unmanaged memory
                    unmanagedBlob = Marshal.AllocHGlobal(Marshal.SizeOf(blob));
                    Marshal.StructureToPtr(blob, unmanagedBlob, fDeleteOld: false);

                    // Invoke the request
                    if (!NativeMethods.CryptMsgControl(
                        _handle,
                        dwFlags: 0,
                        dwCtrlType: CMSG_CONTROL_TYPE.CMSG_CTRL_ADD_CERT,
                        pvCtrlPara: unmanagedBlob))
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                }
                finally
                {
                    NativeUtilities.SafeFree(unmanagedCert);
                    NativeUtilities.SafeFree(unmanagedBlob);
                }
            }
        }

        internal void AddTimestamp(byte[] timeStampCms)
        {
            var unmanagedTimestamp = IntPtr.Zero;
            var unmanagedBlob = IntPtr.Zero;
            var unmanagedAttr = IntPtr.Zero;
            var unmanagedEncoded = IntPtr.Zero;
            var unmanagedAddAttr = IntPtr.Zero;

            try
            {
                // Wrap the timestamp in a CRYPT_INTEGER_BLOB and copy that to unmanaged memory
                unmanagedTimestamp = Marshal.AllocHGlobal(timeStampCms.Length);
                Marshal.Copy(timeStampCms, 0, unmanagedTimestamp, timeStampCms.Length);
                var blob = new CRYPT_INTEGER_BLOB_INTPTR()
                {
                    cbData = (uint)timeStampCms.Length,
                    pbData = unmanagedTimestamp
                };
                unmanagedBlob = Marshal.AllocHGlobal(Marshal.SizeOf(blob));
                Marshal.StructureToPtr(blob, unmanagedBlob, fDeleteOld: false);

                // Wrap it in a CRYPT_ATTRIBUTE and copy that too
                var attr = new CRYPT_ATTRIBUTE()
                {
                    pszObjId = Oids.SignatureTimeStampTokenAttributeOid,
                    cValue = 1,
                    rgValue = unmanagedBlob
                };
                unmanagedAttr = Marshal.AllocHGlobal(Marshal.SizeOf(attr));
                Marshal.StructureToPtr(attr, unmanagedAttr, fDeleteOld: false);

                // Now encode the object using ye olde double-call-to-find-out-the-length mechanism :)
                uint encodedLength = 0;
                if (!NativeMethods.CryptEncodeObjectEx(
                    dwCertEncodingType: NativeMethods.X509_ASN_ENCODING | NativeMethods.PKCS_7_ASN_ENCODING,
                    lpszStructType: new IntPtr(NativeMethods.PKCS_ATTRIBUTE),
                    pvStructInfo: unmanagedAttr,
                    dwFlags: 0,
                    pEncodePara: IntPtr.Zero,
                    pvEncoded: IntPtr.Zero,
                    pcbEncoded: ref encodedLength))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != NativeMethods.ERROR_MORE_DATA)
                    {
                        Marshal.ThrowExceptionForHR(NativeMethods.GetHRForWin32Error(err));
                    }
                }

                unmanagedEncoded = Marshal.AllocHGlobal((int)encodedLength);
                if (!NativeMethods.CryptEncodeObjectEx(
                    dwCertEncodingType: NativeMethods.X509_ASN_ENCODING | NativeMethods.PKCS_7_ASN_ENCODING,
                    lpszStructType: new IntPtr(NativeMethods.PKCS_ATTRIBUTE),
                    pvStructInfo: unmanagedAttr,
                    dwFlags: 0,
                    pEncodePara: IntPtr.Zero,
                    pvEncoded: unmanagedEncoded,
                    pcbEncoded: ref encodedLength))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                // Create the structure used to add the attribute
                var addAttr = new CMSG_CTRL_ADD_SIGNER_UNAUTH_ATTR_PARA()
                {
                    dwSignerIndex = 0,
                    BLOB = new CRYPT_INTEGER_BLOB_INTPTR()
                    {
                        cbData = encodedLength,
                        pbData = unmanagedEncoded
                    }
                };
                addAttr.cbSize = (uint)Marshal.SizeOf(addAttr);
                unmanagedAddAttr = Marshal.AllocHGlobal(Marshal.SizeOf(addAttr));
                Marshal.StructureToPtr(addAttr, unmanagedAddAttr, fDeleteOld: false);

                // Now store the timestamp in the message... FINALLY
                if (!NativeMethods.CryptMsgControl(
                    _handle,
                    dwFlags: 0,
                    dwCtrlType: CMSG_CONTROL_TYPE.CMSG_CTRL_ADD_SIGNER_UNAUTH_ATTR,
                    pvCtrlPara: unmanagedAddAttr))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
            finally
            {
                NativeUtilities.SafeFree(unmanagedTimestamp);
                NativeUtilities.SafeFree(unmanagedBlob);
                NativeUtilities.SafeFree(unmanagedAttr);
                NativeUtilities.SafeFree(unmanagedEncoded);
                NativeUtilities.SafeFree(unmanagedAddAttr);
            }
        }

        internal byte[] Encode()
        {
            return GetByteArrayAttribute(CMSG_GETPARAM_TYPE.CMSG_ENCODED_MESSAGE, index: 0);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
