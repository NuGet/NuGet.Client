// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingBase : IEquatable<SettingBase>
    {
        internal XNode Node { get; set; }

        internal SettingsFile Origin { get; set; }

        internal SettingBase(XNode node, SettingsFile origin)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        protected SettingBase() { }

        /// <summary>
        /// Compares two instances to see if they both match in their values for
        /// the elements unique keys
        /// </summary>
        /// <remarks>Each setting defines it's unique keys,
        /// e.g. if two add items have the same key they are considered equal</remarks>
        /// <param name="other">other instance to compare</param>
        /// <returns>true if both instances refer to the same setting</returns>
        public abstract bool Equals(SettingBase other);

        /// <summary>
        /// Compares two instances for exact equality,
        /// not only checking the unique keys but also any other attributes and values
        /// </summary>
        /// <param name="other"></param>
        /// <returns>true if both instances are exactly the same</returns>
        public abstract bool DeepEquals(SettingBase other);

        /// <summary>
        /// Specifies if the setting is only an in-memory represantion or if
        /// it has a concrete setting in a file.
        /// </summary>
        internal bool IsAbstract() => Node == null;

        /// <summary>
        /// Specifies if the setting has attributes or values.
        /// </summary>
        internal abstract bool IsEmpty();

        /// <summary>
        /// Gives the representation of this setting as an XNode object
        /// </summary>
        internal abstract XNode AsXNode();

        /// <summary>
        /// Creates a shallow copy of the setting.
        /// Does not copy any pointer to the original data structure.
        /// Just copies the abstraction.
        /// </summary>
        internal abstract SettingBase Clone();
    }
}
