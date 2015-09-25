// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Trigger NuGet Event
    /// </summary>
    public class NuGetEventTrigger
    {
        [SuppressMessage(
            "Microsoft.Security",
            "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "The type is immutable.")]
        public static readonly NuGetEventTrigger Instance = new NuGetEventTrigger();

        private readonly TriggerEventMethod _triggerEventMethod;

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "By design, we want to move on if any error occured.")]
        private NuGetEventTrigger()
        {
            try
            {
                var assemblyFile = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    @"EventTrigger.dll");

                if (!File.Exists(assemblyFile))
                {
                    return;
                }

                var assembly = Assembly.Load(AssemblyName.GetAssemblyName(assemblyFile));
                var type = assembly.GetType("EventTrigger");
                if (type == null)
                {
                    return;
                }

                var method = type.GetMethod(
                    "TriggerEvent",
                    BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    return;
                }

                _triggerEventMethod = (TriggerEventMethod)Delegate.CreateDelegate(
                    typeof(TriggerEventMethod), method, throwOnBindFailure: false);
            }
            catch (Exception)
            {
            }
        }

        public void TriggerEvent(int id)
        {
            if (_triggerEventMethod != null)
            {
                _triggerEventMethod(id);
            }
        }

        public static IDisposable TriggerEventBeginEnd(int beginId, int endId)
        {
            return new EventTriggerBeginEnd(beginId, endId);
        }

        private sealed class EventTriggerBeginEnd : IDisposable
        {
            private readonly int _endId;

            public EventTriggerBeginEnd(int beginId, int endId)
            {
                _endId = endId;
                Instance.TriggerEvent(beginId);
            }

            public void Dispose()
            {
                Instance.TriggerEvent(_endId);
            }
        }
    }
}
