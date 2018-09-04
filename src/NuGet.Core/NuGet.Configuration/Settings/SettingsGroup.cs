// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsGroup<T> : SettingElement, ISettingsGroup where T : SettingElement
    {
        // We use a dictionary instead of a hashset to be able to replace elements
        protected Dictionary<T, T> ChildrenSet { get; private set; }

        protected virtual bool CanBeCleared => true;

        protected SettingsGroup()
            : base()
        {
            ChildrenSet = new Dictionary<T, T>();
        }

        protected SettingsGroup(IReadOnlyDictionary<string, string> attributes, IEnumerable<T> children)
            : base(attributes)
        {
            if (children == null)
            {
                ChildrenSet = new Dictionary<T, T>();
            }
            else
            {
                ChildrenSet = new Dictionary<T, T>(children.ToDictionary(c => c, c => c));
            }
        }

        internal override bool IsEmpty() => !ChildrenSet.Any() || ChildrenSet.All(c => c.Value.IsEmpty());

        internal SettingsGroup(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;

            ChildrenSet = SettingFactory.ParseChildren<T>(element, origin, CanBeCleared).ToDictionary(c => c, c => c);
        }

        internal override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name, ChildrenSet.Select(c => c.Value.AsXNode()));

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        /// <summary>
        /// Convenience method to add an Origin to an element and all its children when adding it in a collection
        /// </summary>
        internal override void AddToOrigin(SettingsFile origin)
        {
            Origin = origin;

            foreach (var child in ChildrenSet)
            {
                child.Value.Origin = origin;
            }
        }

        internal virtual bool Add(T setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            Debug.Assert(!IsAbstract());

            if (Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            Debug.Assert(setting.IsAbstract());

            if (!ChildrenSet.ContainsKey(setting) && !setting.IsEmpty())
            {
                ChildrenSet.Add(setting, setting);

                setting.AddToOrigin(Origin);
                setting.Node = setting.AsXNode();

                XElementUtility.AddIndented(Node as XElement, setting.Node);
                setting.Parent = this;
                Origin.IsDirty = true;

                return true;
            }

            return false;
        }

        internal virtual void Remove(T setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            Debug.Assert(!IsAbstract());

            if (Origin != null && Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            Debug.Assert(setting.IsAbstract());

            if (ChildrenSet.TryGetValue(setting, out var currentSetting) && ChildrenSet.Remove(currentSetting))
            {
                Debug.Assert(currentSetting.Origin == Origin);

                XElementUtility.RemoveIndented(currentSetting.Node);
                Origin.IsDirty = true;

                currentSetting.Origin = null;
                currentSetting.Node = null;
                currentSetting.Parent = null;

                if (Parent != null && IsEmpty())
                {
                    Parent.Remove(this);
                }
            }
        }

        void ISettingsGroup.Remove(SettingElement setting)
        {
            Remove(setting as T);
        }
    }
}
