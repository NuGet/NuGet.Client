using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NuGet.Client
{
    // Portable Exceptions can't be Serializable :(

    /// <summary>
    /// The exception thrown when an error occurs during an operation in the NuGet Client library
    /// </summary>
    public class NuGetClientException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class.
        /// </summary>
        public NuGetClientException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public NuGetClientException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public NuGetClientException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// The exception thrown when an error occurs while interpreting JSON data returned by a NuGet Service
    /// </summary>
    public class NuGetInvalidJsonException : NuGetClientException
    {
        /// <summary>
        /// Gets the <see cref="Newtonsoft.Json.Linq.JToken"/> that was invalid.
        /// </summary>
        public JToken InvalidToken { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class.
        /// </summary>
        public NuGetInvalidJsonException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public NuGetInvalidJsonException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message and a reference to the <see cref="JToken"/> that was invalid.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="invalidToken">The <see cref="JToken"/> that was invalid.</param>
        public NuGetInvalidJsonException(string message, JToken invalidToken) : base(message) { InvalidToken = invalidToken; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public NuGetInvalidJsonException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetClientException"/> class
        /// with a specified error message, a reference to the inner exception that is the cause of this exception and a reference to the <see cref="JToken"/> that was invalid.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="invalidToken">The <see cref="JToken"/> that was invalid.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public NuGetInvalidJsonException(string message, JToken invalidToken, Exception innerException) : base(message, innerException) { InvalidToken = invalidToken; }
    }
}
