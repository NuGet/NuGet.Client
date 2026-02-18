// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if NETFRAMEWORK
#nullable enable

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Suppresses reporting of a specific rule violation, allowing multiple suppressions on a single code artifact.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute class, specifying the category of the tool and the identifier for an analysis rule.
        /// </summary>
        /// <param name="category">The category for the attribute.</param>
        /// <param name="checkId">The identifier of the analysis rule the attribute applies to.</param>
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        /// <summary>
        /// Gets the category identifying the classification of the attribute.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the identifier of the analysis tool rule to be suppressed.
        /// </summary>
        public string CheckId { get; }

        /// <summary>
        /// Gets or sets the justification for suppressing the code analysis message.
        /// </summary>
        public string? Justification { get; set; }

        /// <summary>
        /// Gets or sets an optional argument expanding on exclusion criteria.
        /// </summary>
        public string? MessageId { get; set; }

        /// <summary>
        /// Gets or sets the scope of the code that is relevant for the attribute.
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets a fully qualified path that represents the target of the attribute.
        /// </summary>
        public string? Target { get; set; }
    }

    /// <summary>
    /// Indicates that certain members on a specified System.Type are accessed dynamically, for example, through System.Reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute class with the specified member types.
        /// </summary>
        /// <param name="memberTypes">The types of the dynamically accessed members.</param>
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }

        /// <summary>
        /// Gets the System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes that specifies the type of dynamically accessed members.
        /// </summary>
        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    /// <summary>
    /// Specifies the types of members on a System.Type that are accessed dynamically.
    /// </summary>
    internal enum DynamicallyAccessedMemberTypes
    {
        /// <summary>
        /// Specifies all members.
        /// </summary>
        All = -1,
        /// <summary>
        /// Specifies no members.
        /// </summary>
        None = 0,
        /// <summary>
        /// Specifies the default, parameterless public constructor.
        /// </summary>
        PublicParameterlessConstructor = 1,
        /// <summary>
        /// Specifies all public constructors.
        /// </summary>
        PublicConstructors = 3,
        /// <summary>
        /// Specifies all non-public constructors.
        /// </summary>
        NonPublicConstructors = 4,
        /// <summary>
        /// Specifies all public methods.
        /// </summary>
        PublicMethods = 8,
        /// <summary>
        /// Specifies all non-public methods.
        /// </summary>
        NonPublicMethods = 16,
        /// <summary>
        /// Specifies all public fields.
        /// </summary>
        PublicFields = 32,
        /// <summary>
        /// Specifies all non-public fields.
        /// </summary>
        NonPublicFields = 64,
        /// <summary>
        /// Specifies all public nested types.
        /// </summary>
        PublicNestedTypes = 128,
        /// <summary>
        /// Specifies all non-public nested types.
        /// </summary>
        NonPublicNestedTypes = 256,
        /// <summary>
        /// Specifies all public properties.
        /// </summary>
        PublicProperties = 512,
        /// <summary>
        /// Specifies all non-public properties.
        /// </summary>
        NonPublicProperties = 1024,
        /// <summary>
        /// Specifies all public events.
        /// </summary>
        PublicEvents = 2048,
        /// <summary>
        /// Specifies all non-public events.
        /// </summary>
        NonPublicEvents = 4096,
        /// <summary>
        /// Specifies all interfaces implemented by the type.
        /// </summary>
        Interfaces = 8192
    }
}
#endif