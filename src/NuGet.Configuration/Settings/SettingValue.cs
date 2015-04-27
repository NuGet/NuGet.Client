using System;

namespace NuGet.Configuration
{
    /// <summary>
    /// Represents a single setting value in a settings file
    /// </summary>
    public class SettingValue
    {
        public SettingValue(string key, string value, bool isMachineWide, int priority = 0)
        {
            Key = key;
            Value = value;
            IsMachineWide = isMachineWide;
            Priority = priority;
        }

        /// <summary>
        /// Represents the key of the setting
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Represents the value of the setting
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// IsMachineWide tells if the setting is machine-wide or not
        /// </summary>
        public bool IsMachineWide { get; private set; }

        /// <summary>
        /// The priority of this setting in the nuget.config hierarchy. Bigger number means higher priority
        /// </summary>
        public int Priority { get; private set; }

        public override bool Equals(object obj)
        {
            var rhs = obj as SettingValue;
            if (rhs == null)
            {
                return false;
            }

            return rhs.Key == Key &&
                rhs.Value == Value &&
                rhs.IsMachineWide == rhs.IsMachineWide;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(Key, Value, IsMachineWide).GetHashCode();
        }
    }

}
