using System;
using NuGet.CommandLine;

namespace NuGet
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class OptionAttribute : Attribute
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private string _description;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool IsHidden { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string AltName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string DescriptionResourceName { get; private set; }
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
        public Type ResourceType { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public OptionAttribute(string description)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            Description = description;
            IsHidden = false;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public OptionAttribute(Type resourceType, string descriptionResourceName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            ResourceType = resourceType;
            DescriptionResourceName = descriptionResourceName;
            IsHidden = false;
        }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public OptionAttribute(string description, bool isHidden)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
            : this(description)
        {
            IsHidden = isHidden;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public OptionAttribute(Type resourceType, string descriptionResourceName, bool isHidden)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
            : this(resourceType, descriptionResourceName)
        {
            IsHidden = isHidden;
        }
    }
}
