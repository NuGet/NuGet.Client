// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.RuntimeModel
{
    /// <summary>
    /// Generates JSON from an object graph.
    ///
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class JsonObjectWriter : IObjectWriter
    {
        private readonly Stack<JContainer> _containers;
        private JContainer _currentContainer;
        private bool _isReadOnly;
        private readonly JObject _root;

        public JsonObjectWriter()
        {
            _containers = new Stack<JContainer>();
            _root = new JObject();

            _currentContainer = _root;
        }

        public void WriteObjectStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _containers.Push(_currentContainer);

            var newContainer = new JObject();

            _currentContainer[name] = newContainer;
            _currentContainer = newContainer;
        }

        public void WriteObjectInArrayStart()
        {
            ThrowIfReadOnly();

            _containers.Push(_currentContainer);

            var newContainer = new JObject();

            _currentContainer.Add(newContainer);
            _currentContainer = newContainer;
        }

        public void WriteArrayStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _containers.Push(_currentContainer);

            var newContainer = new JArray();

            _currentContainer[name] = newContainer;
            _currentContainer = newContainer;
        }

        public void WriteObjectEnd()
        {
            ThrowIfReadOnly();

            if (_currentContainer == _root)
            {
                throw new InvalidOperationException();
            }

            _currentContainer = GetPreviousContainer();
        }

        public void WriteArrayEnd()
        {
            ThrowIfReadOnly();

            if (_currentContainer == _root)
            {
                throw new InvalidOperationException();
            }

            _currentContainer = GetPreviousContainer();
        }

        public void WriteNameValue(string name, int value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _currentContainer[name] = value;
        }

        public void WriteNameValue(string name, bool value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _currentContainer[name] = value;
        }

        public void WriteNameValue(string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _currentContainer[name] = value;
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfReadOnly();

            _currentContainer[name] = new JArray(values);
        }

        /// <summary>
        /// Gets the JSON for the object.
        ///
        /// Once <see cref="GetJson"/> is called, no further writing is allowed.
        /// </summary>
        public string GetJson()
        {
            _isReadOnly = true;

            return _root.ToString();
        }

        /// <summary>
        /// Gets the JObject (in-memory JSON model) for the object.
        /// 
        /// Once <see cref="GetJObject"/> is called, no further writing is allowed.
        /// </summary>
        /// <returns></returns>
        public JObject GetJObject()
        {
            _isReadOnly = true;

            return _root;
        }

        /// <summary>
        /// Writes the result to a <c>JsonTextWriter</c>.
        ///
        /// Once WriteTo is called, no further writing is allowed.
        /// </summary>
        public void WriteTo(JsonTextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _isReadOnly = true;

            _root.WriteTo(writer);
        }

        private JContainer GetPreviousContainer()
        {
            if (_containers.Count == 0)
            {
                return null;
            }

            return _containers.Pop();
        }

        private void ThrowIfReadOnly()
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException();
            }
        }
    }
}