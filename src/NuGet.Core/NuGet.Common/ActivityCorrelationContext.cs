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
    /// <summary>
    /// Ambient context contains properties pertaining to a current activity.
    /// Single activity engages multiple method calls at different layers. 
    /// Sometimes it's necessary to identify separate calls belonging to the same activity if shared state is needed.
    /// </summary>
    public class ActivityCorrelationContext
    {
        private static readonly string DataSlotId = "NuGet.ActivityCorrelationContext";

#if DNXCORE50
        private static readonly AsyncLocal<ActivityCorrelationContext> _instance = new AsyncLocal<ActivityCorrelationContext>();
#endif

        private static readonly ActivityCorrelationContext _globalInstance = new ActivityCorrelationContext
        {
            CorrelationId = default(Guid).ToString()
        };

        /// <summary>
        /// Represents activity unique ID.
        /// </summary>
        public string CorrelationId { get; private set; }

        /// <summary>
        /// Returns ambient context value or global instance if not set earlier.
        /// </summary>
        public static ActivityCorrelationContext Current
        {
            get
            {
#if DNXCORE50
                return _instance.Value ?? _globalInstance;
#else
                var context = CallContext.LogicalGetData(DataSlotId);
                return (ActivityCorrelationContext)context ?? _globalInstance;
#endif
            }
        }

        /// <summary>
        /// Instantiates activity context instance with new identifier. Updates ambient context value.
        /// </summary>
        /// <returns>New instance</returns>
        public static ActivityCorrelationContext StartNew()
        {
            var context = new ActivityCorrelationContext
            {
                CorrelationId = Guid.NewGuid().ToString()
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
