// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NuGet.Common;

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

        internal static byte[] GetSignatureValueHash(HashAlgorithmName hashAlgorithmName, NativeCms nativeCms)
        {
            var signatureValue = nativeCms.GetEncryptedDigest();

            using (var signatureValueStream = new MemoryStream(signatureValue))
            using (var hashAlgorithm = hashAlgorithmName.GetHashProvider())
            {
                return hashAlgorithm.ComputeHash(signatureValueStream, leaveStreamOpen: false);
            }
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
                using (var hb = new HeapBlockRetainer())
                {
                    var unmanagedCert = hb.Alloc(cert.Length);
                    Marshal.Copy(cert, 0, unmanagedCert, cert.Length);
                    var blob = new CRYPT_INTEGER_BLOB()
                    {
                        cbData = (uint)cert.Length,
                        pbData = unmanagedCert
                    };

                    var unmanagedBlob = hb.Alloc(Marshal.SizeOf(blob));
                    Marshal.StructureToPtr(blob, unmanagedBlob, fDeleteOld: false);

                    if (!NativeMethods.CryptMsgControl(
                        _handle,
                        dwFlags: 0,
                        dwCtrlType: CMSG_CONTROL_TYPE.CMSG_CTRL_ADD_CERT,
                        pvCtrlPara: unmanagedBlob))
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                }
            }
        }

        internal unsafe void AddTimestamp(byte[] timeStampCms)
        {
            using (var hb = new HeapBlockRetainer())
            {
                var unmanagedTimestamp = hb.Alloc(timeStampCms.Length);
                Marshal.Copy(timeStampCms, 0, unmanagedTimestamp, timeStampCms.Length);
                var blob = new CRYPT_INTEGER_BLOB()
                {
                    cbData = (uint)timeStampCms.Length,
                    pbData = unmanagedTimestamp
                };
                var unmanagedBlob = hb.Alloc(Marshal.SizeOf(blob));
                Marshal.StructureToPtr(blob, unmanagedBlob, fDeleteOld: false);

                var attr = new CRYPT_ATTRIBUTE()
                {
                    pszObjId = hb.AllocAsciiString(Oids.SignatureTimeStampTokenAttribute),
                    cValue = 1,
                    rgValue = unmanagedBlob
                };
                var unmanagedAttr = hb.Alloc(Marshal.SizeOf(attr));
                Marshal.StructureToPtr(attr, unmanagedAttr, fDeleteOld: false);

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

                var unmanagedEncoded = hb.Alloc((int)encodedLength);
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

                var addAttr = new CMSG_CTRL_ADD_SIGNER_UNAUTH_ATTR_PARA()
                {
                    dwSignerIndex = 0,
                    BLOB = new CRYPT_INTEGER_BLOB()
                    {
                        cbData = encodedLength,
                        pbData = unmanagedEncoded
                    }
                };
                addAttr.cbSize = (uint)Marshal.SizeOf(addAttr);
                var unmanagedAddAttr = hb.Alloc(Marshal.SizeOf(addAttr));
                Marshal.StructureToPtr(addAttr, unmanagedAddAttr, fDeleteOld: false);

                if (!NativeMethods.CryptMsgControl(
                    _handle,
                    dwFlags: 0,
                    dwCtrlType: CMSG_CONTROL_TYPE.CMSG_CTRL_ADD_SIGNER_UNAUTH_ATTR,
                    pvCtrlPara: unmanagedAddAttr))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
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