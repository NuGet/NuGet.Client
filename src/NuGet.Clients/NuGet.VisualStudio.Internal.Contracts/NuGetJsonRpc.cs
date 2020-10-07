// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace NuGet.VisualStudio.Internal.Contracts
{
    // Unsealed only to facilitate testing.
    internal class NuGetJsonRpc : JsonRpc
    {
        internal NuGetJsonRpc(IJsonRpcMessageHandler messageHandler)
            : base(messageHandler)
        {
        }

        protected override Type GetErrorDetailsDataType(JsonRpcError error)
        {
            if (error is object
                && error.Error is object
                && (int)error.Error.Code == (int)RemoteErrorCode.RemoteError)
            {
                return typeof(RemoteError);
            }

            return base.GetErrorDetailsDataType(error);
        }
    }
}
