// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingBase : IEquatable<SettingBase>
    {
        internal XNode Node { get; private set; }

        internal ISettingsGroup Parent { get; set; }

        internal SettingsFile Origin { get; private set; }

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
        /// <remarks>Each setting defines its unique keys,
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
        /// Specifies if the setting is an in-memory-only setting. 
        /// </summary>
        internal bool IsAbstract() => Node == null && Origin == null;

        /// <summary>
        /// Specifies if the setting is a copy of a concrete setting in a file.
        /// </summary>
        internal bool IsCopy() => Node == null && Origin != null;

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

        internal void SetNode(XNode node)
        {
            if (Node != null)
            {
                throw new InvalidOperationException(Resources.CannotUpdateNode);
            }

            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        /// <summary>
        /// Convenience method to add an element to an origin
        /// </summary>
        /// <remarks>Each setting can override this method to inlcude any descendants to the origin</remarks>
        internal virtual void SetOrigin(SettingsFile origin)
        {
            if (Origin != null)
            {
                throw new InvalidOperationException(Resources.CannotUpdateOrigin);
            }

            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        /// <summary>
        /// Convenience method to remove an element from it's origin and convert to abstract
        /// </summary>
        /// <remarks>Each setting can override this method to remove any descendants from their origin</remarks>
        internal virtual void RemoveFromSettings()
        {
            if (!IsCopy() && !IsAbstract())
            {
                XElementUtility.RemoveIndented(Node);
                Origin.IsDirty = true;

                Node = null;
            }

            Origin = null;
            Parent = null;
        }
    }
}
