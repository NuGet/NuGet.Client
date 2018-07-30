// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsNode : IEquatable<SettingsNode>, ISettingsNode
    {
        internal XNode Node { get; set; }

        internal ISettingsCollection Parent { get; set; }

        internal ISettingsFile Origin { get; set; }

        public bool IsAbstract => Origin == null && Node == null;

        internal SettingsNode MergedWith { get; set; } 

        XNode ISettingsNode.Node => Node;

        ISettingsCollection ISettingsNode.Parent => Parent;

        ISettingsFile ISettingsNode.Origin => Origin;

        public abstract bool IsEmpty();

        protected SettingsNode()
        {
        }

        internal SettingsNode(XNode node, ISettingsFile origin)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        /// <summary>
        /// Compares two instances to see if they both match in their values for
        /// the elements unique keys
        /// </summary>
        /// <remarks>Each element defines it's unique keys,
        /// e.g. if two add items have the same key they are considered equal</remarks>
        /// <param name="other">other instance to compare</param>
        /// <returns>true if both instances refer to the same node</returns>
        public abstract bool Equals(SettingsNode other);

        /// <summary>
        /// Compares two instances for exact equality,
        /// not only checking the unique keys but also any other attributes and values
        /// </summary>
        /// <param name="other"></param>
        /// <returns>true if both instances refer to the same node and have the same values</returns>
        public abstract bool DeepEquals(SettingsNode other);

        public abstract XNode AsXNode();

        public bool RemoveFromCollection(bool isBatchOperation = false)
        {
            if (Parent != null)
            {
                return Parent.RemoveChild(this, isBatchOperation);
            }

            return false;
        }

        internal virtual void AddToOrigin(ISettingsFile origin)
        {
            Origin = origin;
        }
    }
}
