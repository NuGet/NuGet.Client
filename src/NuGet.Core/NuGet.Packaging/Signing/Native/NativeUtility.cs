// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
#if IS_DESKTOP
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NuGet.Common;
#endif

namespace NuGet.Packaging.Signing
{
    internal static class NativeUtilities
    {
        internal static void SafeFree(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        internal static void ThrowIfFailed(bool result)
        {
            if (!result)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }
#if IS_DESKTOP
        internal static SignedCms NativeSign(
            byte[] data,
            X509Certificate2 certificate,
            CngKey privateKey,
            CryptographicAttributeObjectCollection attributes,
            Common.HashAlgorithmName hashAlgorithm,
            X509Certificate2Collection additionalCertificates)
        {
            using (var hb = new HeapBlockRetainer())
            using (var chain = new X509Chain())
            {
                SigningUtility.SetCertBuildChainPolicy(chain, additionalCertificates, DateTime.Now, NuGetVerificationCertificateType.Signature);

                if (!chain.Build(certificate))
                {
                    foreach (var chainStatus in chain.ChainStatus)
                    {
                        if (chainStatus.Status != X509ChainStatusFlags.NoError)
                        {
                            throw new SignatureException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, chainStatus.Status.ToString()));
                        }
                    }
                }

                var certificateBlobs = new BLOB[chain.ChainElements.Count];

                for (var i = 0; i < chain.ChainElements.Count; ++i)
                {
                    var cert = chain.ChainElements[i].Certificate;
                    var context = Marshal.PtrToStructure<CERT_CONTEXT>(cert.Handle);

                    certificateBlobs[i] = new BLOB() { cbData = context.cbCertEncoded, pbData = context.pbCertEncoded };
                }

                byte[] encodedData;
                var signerInfo = CreateEncodeInfo(certificate, privateKey, attributes, hashAlgorithm, hb);

                var signedInfo = new CMSG_SIGNED_ENCODE_INFO();
                signedInfo.cbSize = Marshal.SizeOf(signedInfo);
                signedInfo.cSigners = 1;

                using (var signerInfoHandle = new SafeLocalAllocHandle(Marshal.AllocHGlobal(Marshal.SizeOf(signerInfo))))
                {
                    Marshal.StructureToPtr(signerInfo, signerInfoHandle.DangerousGetHandle(), fDeleteOld: false);

                    signedInfo.rgSigners = signerInfoHandle.DangerousGetHandle();
                    signedInfo.cCertEncoded = certificateBlobs.Length;

                    using (var certificatesHandle = new SafeLocalAllocHandle(Marshal.AllocHGlobal(Marshal.SizeOf(certificateBlobs[0]) * certificateBlobs.Length)))
                    {
                        for (var i = 0; i < certificateBlobs.Length; ++i)
                        {
                            Marshal.StructureToPtr(certificateBlobs[i], new IntPtr(certificatesHandle.DangerousGetHandle().ToInt64() + Marshal.SizeOf(certificateBlobs[i]) * i), fDeleteOld: false);
                        }

                        signedInfo.rgCertEncoded = certificatesHandle.DangerousGetHandle();

                        var hMsg = NativeMethods.CryptMsgOpenToEncode(
                            NativeMethods.X509_ASN_ENCODING | NativeMethods.PKCS_7_ASN_ENCODING,
                            dwFlags: 0,
                            dwMsgType: NativeMethods.CMSG_SIGNED,
                            pvMsgEncodeInfo: ref signedInfo,
                            pszInnerContentObjID: null,
                            pStreamInfo: IntPtr.Zero);

                        ThrowIfFailed(!hMsg.IsInvalid);

                        ThrowIfFailed(NativeMethods.CryptMsgUpdate(
                            hMsg,
                            data,
                            (uint)data.Length,
                            fFinal: true));

                        uint valueLength = 0;

                        ThrowIfFailed(NativeMethods.CryptMsgGetParam(
                            hMsg,
                            CMSG_GETPARAM_TYPE.CMSG_CONTENT_PARAM,
                            dwIndex: 0,
                            pvData: null,
                            pcbData: ref valueLength));

                        encodedData = new byte[(int)valueLength];

                        ThrowIfFailed(NativeMethods.CryptMsgGetParam(
                            hMsg,
                            CMSG_GETPARAM_TYPE.CMSG_CONTENT_PARAM,
                            dwIndex: 0,
                            pvData: encodedData,
                            pcbData: ref valueLength));
                    }
                }

                var cms = new SignedCms();

                cms.Decode(encodedData);

                return cms;
            }
        }

        private unsafe static CMSG_SIGNER_ENCODE_INFO CreateEncodeInfo(
            X509Certificate2 certificate,
            CngKey privateKey,
            CryptographicAttributeObjectCollection attributes,
            Common.HashAlgorithmName hashAlgorithm,
            HeapBlockRetainer hb)
        {
            var signerInfo = new CMSG_SIGNER_ENCODE_INFO();
            signerInfo.cbSize = (uint)Marshal.SizeOf(signerInfo);
            signerInfo.pCertInfo = Marshal.PtrToStructure<CERT_CONTEXT>(certificate.Handle).pCertInfo;
            signerInfo.hCryptProvOrhNCryptKey = privateKey.Handle.DangerousGetHandle();
            signerInfo.HashAlgorithm.pszObjId = hashAlgorithm.ConvertToOidString();

            if (attributes.Count != 0)
            {
                signerInfo.cAuthAttr = attributes.Count;

                checked
                {
                    var attributeSize = Marshal.SizeOf<CRYPT_ATTRIBUTE>();
                    var blobSize = Marshal.SizeOf<CRYPT_INTEGER_BLOB_INTPTR>();
                    var attributesArray = (CRYPT_ATTRIBUTE*)hb.Alloc(attributeSize * attributes.Count);
                    var currentAttribute = attributesArray;

                    foreach (var attribute in attributes)
                    {
                        var dataBlob = (CRYPT_INTEGER_BLOB_INTPTR*)hb.Alloc(blobSize);

                        currentAttribute->pszObjId = hb.AllocAsciiString(attribute.Oid.Value);
                        currentAttribute->cValue = (uint)attribute.Values.Count;
                        currentAttribute->rgValue = (IntPtr)dataBlob;

                        foreach (var value in attribute.Values)
                        {
                            var attrData = value.RawData;

                            if (attrData.Length > 0)
                            {
                                var rawData = hb.Alloc(value.RawData.Length);

                                // Assign data to datablob
                                dataBlob->cbData = (uint)attrData.Length;
                                dataBlob->pbData = rawData;

                                Marshal.Copy(attrData, 0, rawData, attrData.Length);
                            }
                        }

                        currentAttribute++;
                    }
                    signerInfo.rgAuthAttr = new IntPtr(attributesArray);
                }
            }
            return signerInfo;
        }
#endif
    }
}