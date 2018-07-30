// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsCollection<T> : SettingsElement, ISettingsCollection where T : SettingsNode
    {
        // We use a dictionary instead of a hashset to be able to replace elements
        protected Dictionary<T, T> ChildrenSet { get; private set; }

        public IReadOnlyCollection<T> Children => ChildrenSet.Select(c => c.Value).ToList();

        protected virtual bool CanBeCleared => true;

        private bool _isCleared { get; set; }

        public override bool IsEmpty() => !ChildrenSet.Any() || ChildrenSet.All(c => c.Value.IsEmpty());

        public SettingsCollection()
            : base()
        {
            ChildrenSet = new Dictionary<T, T>();
        }

        public SettingsCollection(IEnumerable<T> children)
            : base()
        {
            ChildrenSet = children.ToDictionary(c => c, c => c);
        }

        internal SettingsCollection(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;

            ChildrenSet = SettingFactory.ParseChildren<T>(element, origin, CanBeCleared).ToDictionary(c => c, c => c);

            foreach (var child in ChildrenSet)
            {
                child.Value.Parent = this;
            }
        }

        public override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name);

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            foreach (var child in ChildrenSet)
            {
                XElementUtility.AddIndented(element, child.Value.AsXNode());
            }

            Node = element;

            return Node;
        }

        bool ISettingsCollection.RemoveChild(SettingsNode child, bool isBatchOperation)
        {
            return RemoveChild(child as T, isBatchOperation);
        }

        public bool RemoveChild(T child, bool isBatchOperation = false)
        {
            if (child == null || (child.Origin != null && child.Origin.IsMachineWide))
            {
                return false;
            }

            if (ChildrenSet.ContainsKey(child) && ChildrenSet.Remove(child))
            {
                child.Parent = null;
                var successfullyRemoved = true;

                if (!child.IsAbstract)
                {
                    XElementUtility.RemoveIndented(child.Node);
                    child.Origin.IsDirty = true;

                    if (!isBatchOperation)
                    {
                        child.Origin.Save();
                    }

                    child.Origin = null;
                    child.Node = null;
                }

                if (child.MergedWith != null)
                {
                    successfullyRemoved &= child.MergedWith.RemoveFromCollection(isBatchOperation);
                }

                if (Parent != null && IsEmpty())
                {
                    successfullyRemoved &= RemoveFromCollection(isBatchOperation);
                }

                return successfullyRemoved;
            }

            return false;
        }

        internal override void AddToOrigin(ISettingsFile origin)
        {
            Origin = origin;

            foreach(var child in ChildrenSet)
            {
                child.Value.Origin = origin;
            }
        }

        bool ISettingsCollection.AddChild(SettingsNode child, bool isBatchOperation)
        {
            return AddChild(child as T, isBatchOperation);
        }

        public bool AddChild(T child, bool isBatchOperation = false)
        {
            if (child != null && !child.IsEmpty())
            {
                if (!child.IsAbstract)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_AddChildAlreadyHasOrigin, child.Origin.ConfigFilePath));
                }

                if (Origin != null && Origin.IsMachineWide)
                {
                    return false;
                }

                if (!ChildrenSet.ContainsKey(child))
                {
                    ChildrenSet.Add(child, child);

                    if (Origin != null)
                    {
                        child.AddToOrigin(Origin);

                        XElementUtility.AddIndented(Node as XElement, child.AsXNode());
                        Origin.IsDirty = true;

                        if (!isBatchOperation)
                        {
                            Origin.Save();
                        }
                    }

                    child.Parent = this;

                    return true;
                }
            }

            return false;
        }
    }
}
