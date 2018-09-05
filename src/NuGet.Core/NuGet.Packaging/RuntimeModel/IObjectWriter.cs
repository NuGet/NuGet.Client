// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.RuntimeModel
{
    /// <summary>
    /// Provides functionality for writing an object graph.
    /// The output format is defined by implementors.
    /// </summary>
    public interface IObjectWriter
    {
        /// <summary>
        /// Writes the start of a nested object.
        ///
        /// This new object becomes the scope for all other method calls until either WriteObjectStart
        /// is called again to start a new nested object or WriteObjectEnd is called.
        ///
        /// Every call to WriteObjectStart must be balanced by a corresponding call to WriteObjectEnd.
        /// </summary>
        /// <param name="name">The name of the object.  Throws if <c>null</c>.</param>
        void WriteObjectStart(string name);

        /// <summary>
        /// Writes the end of a nested object.
        ///
        /// The parent object for this object becomes the scope for subsequent method calls.
        /// If this object is the root object, no further writing is allowed.
        ///
        /// Every call to WriteObjectStart must be balanced by a corresponding call to WriteObjectEnd.
        /// </summary>
        void WriteObjectEnd();

        /// <summary>
        /// Writes a name-value pair.
        /// </summary>
        /// <param name="name">The name of the datum.  Throws if <c>null</c>.</param>
        /// <param name="value">The datum.</param>
        void WriteNameValue(string name, int value);

        /// <summary>
        /// Writes a name-value pair.
        /// </summary>
        /// <param name="name">The name of the datum.  Throws if <c>null</c>.</param>
        /// <param name="value">The datum.</param>
        void WriteNameValue(string name, bool value);

        /// <summary>
        /// Writes a name-value pair.
        /// </summary>
        /// <param name="name">The name of the datum.  Throws if <c>null</c>.</param>
        /// <param name="value">The datum.</param>
        void WriteNameValue(string name, string value);

        /// <summary>
        /// Writes a name-collection pair.
        /// </summary>
        /// <param name="name">The name of the data.  Throws if <c>null</c>.</param>
        /// <param name="values">The data.</param>
        void WriteNameArray(string name, IEnumerable<string> values);
    }
}