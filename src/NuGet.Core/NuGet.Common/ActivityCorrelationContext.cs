// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

#if DNXCORE50
using System.Threading;
#else
using System.Runtime.Remoting.Messaging;
#endif

namespace NuGet.Common
{
    public class ActivityCorrelationContext
    {
        private static readonly string DataSlotId = "NuGet.ActivityCorrelationContext";

#if DNXCORE50
        private static readonly AsyncLocal<ActivityCorrelationContext> _instance = new AsyncLocal<ActivityCorrelationContext>();
#endif

        public string CorrelationId { get; private set; }

        public static ActivityCorrelationContext Current
        {
            get
            {
#if DNXCORE50
                return _instance.Value;
#else
                var context = CallContext.LogicalGetData(DataSlotId);
                return (ActivityCorrelationContext)context;
#endif
            }
        }

        public static ActivityCorrelationContext StartNew()
        {
            var context = new ActivityCorrelationContext
            {
                CorrelationId = Guid.NewGuid().ToString("N")
            };

#if DNXCORE50
            _instance.Value = context;
#else
            CallContext.LogicalSetData(DataSlotId, context);
#endif
            return context;
        }
    }
}
