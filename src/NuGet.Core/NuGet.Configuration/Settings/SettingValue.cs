// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    /// <summary>
    /// Represents a single setting value in a settings file
    /// </summary>
    public class SettingValue
    {
        public SettingValue(string key, 
                            string value, 
                            bool isMachineWide, 
                            int priority = 0)
            : this(key, 
                  value, 
                  origin: null, 
                  isMachineWide: isMachineWide,
                  originalValue: value,
                  priority: priority)
        { }

        public SettingValue(string key,
                            string value,
                            ISettings origin,
                            bool isMachineWide,
                            int priority = 0) 
            : this(key,
                   value,
                   origin: origin,
                   isMachineWide : isMachineWide,
                   originalValue : value,
                   priority: priority)
        { }

        public SettingValue(string key, 
                            string value, 
                            ISettings origin, 
                            bool isMachineWide,
                            string originalValue,
                            int priority = 0)
        {
            Key = key;
            Value = value;
            Origin = origin;
            IsMachineWide = isMachineWide;
            Priority = priority;
            OriginalValue = originalValue;
            AdditionalData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        

        /// <summary>
        /// Represents the key of the setting
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Represents the value of the setting
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// original value of the source as in NuGet.Config
        /// </summary>
        public string OriginalValue { get; set; }

        /// <summary>
        /// IsMachineWide tells if the setting is machine-wide or not
        /// </summary>
        public bool IsMachineWide { get; set; }

        /// <summary>
        /// The priority of this setting in the nuget.config hierarchy. Bigger number means higher priority
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets the <see cref="ISettings"/> that provided this value.
        /// </summary>
        public ISettings Origin { get; }

        /// <summary>
        /// Gets additional values with the specified setting.
        /// </summary>
        /// <remarks>
        /// When reading from an XML based settings file, this includes all attributes on the element
        /// other than the <c>Key</c> and <c>Value</c>.
        /// </remarks>
        public IDictionary<string, string> AdditionalData { get; }

        public override bool Equals(object obj)
        {
            var rhs = obj as SettingValue;

            if (rhs != null
                &&
                string.Equals(rhs.Key, Key, StringComparison.OrdinalIgnoreCase)
                &&
                string.Equals(rhs.Value, Value, StringComparison.OrdinalIgnoreCase)
                &&
                rhs.IsMachineWide == IsMachineWide
                &&
                rhs.AdditionalData.Count == AdditionalData.Count)
            {
                return Enumerable.SequenceEqual(
                    AdditionalData.OrderBy(data => data.Key, StringComparer.OrdinalIgnoreCase),
                    rhs.AdditionalData.OrderBy(data => data.Key, StringComparer.OrdinalIgnoreCase));
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(Key, Value, IsMachineWide).GetHashCode();
        }
    }
}
