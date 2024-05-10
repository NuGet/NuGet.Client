using System;
using NuGet.CommandLine;

namespace NuGet
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class CommandAttribute : Attribute
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private string _description;
        private string _usageSummary;
        private string _usageDescription;
        private string _example;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CommandName { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Type ResourceType { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string DescriptionResourceName { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string AltName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public int MinArgs { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public int MaxArgs { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageSummaryResourceName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageDescriptionResourceName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageExampleResourceName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Description
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(DescriptionResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, DescriptionResourceName);
                }
                return _description;
            }
            private set
            {
                _description = value;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageSummary
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageSummaryResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageSummaryResourceName);
                }
                return _usageSummary;
            }
            set
            {
                _usageSummary = value;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageDescription
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageDescriptionResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageDescriptionResourceName);
                }
                return _usageDescription;
            }
            set
            {
                _usageDescription = value;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string UsageExample
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageExampleResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageExampleResourceName);
                }
                return _example;
            }
            set
            {
                _example = value;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public CommandAttribute(string commandName, string description)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            CommandName = commandName;
            Description = description;
            MinArgs = 0;
            MaxArgs = Int32.MaxValue;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public CommandAttribute(Type resourceType, string commandName, string descriptionResourceName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            ResourceType = resourceType;
            CommandName = commandName;
            DescriptionResourceName = descriptionResourceName;
            MinArgs = 0;
            MaxArgs = Int32.MaxValue;
        }
    }
}
