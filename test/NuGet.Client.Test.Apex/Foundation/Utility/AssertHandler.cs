using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace NuGetClient.Test.Foundation.Utility
{
    /// <summary>
    /// Swallow asserts and throw an exception, xUnit v1 used to wrap all test commands with a TraceListener that would do this, now we are replicating it
    /// </summary>
    public sealed class AssertExceptionHandler : TraceListener
    {
        private const string AssertHandlerName = "NuGetClient.Test.Foundation.Utility.AssertExceptionHandler";
        List<TraceListener> oldListeners = new List<TraceListener>();

        public AssertExceptionHandler() : this(AssertExceptionHandler.AssertHandlerName) { }

        public AssertExceptionHandler(string name) : base(name)
        {
            try
            {
                foreach (TraceListener oldListener in Trace.Listeners)
                {
                    oldListeners.Add(oldListener);
                }

                Trace.Listeners.Clear();
                Trace.Listeners.Add(this);

            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                Trace.Listeners.Clear();
                Trace.Listeners.AddRange(oldListeners.ToArray());
            }
            catch (Exception) { }

        }

        public override void Fail(string message, string detailMessage)
        {
            throw new TraceAssertException(message, detailMessage);
        }

        public override void Write(string message) { }

        public override void WriteLine(string message) { }
    }

    [Serializable]
    public class TraceAssertException : Exception
    {
        readonly string assertDetailedMessage;
        readonly string assertMessage;

        /// <summary>
        /// Creates a new instance of the <see cref="TraceAssertException"/> class.
        /// </summary>
        /// <param name="assertMessage">The original assert message</param>
        public TraceAssertException(string assertMessage)
            : this(assertMessage, "")
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="TraceAssertException"/> class.
        /// </summary>
        /// <param name="assertMessage">The original assert message</param>
        /// <param name="assertDetailedMessage">The original assert detailed message</param>
        public TraceAssertException(string assertMessage, string assertDetailedMessage)
        {
            this.assertMessage = assertMessage ?? "";
            this.assertDetailedMessage = assertDetailedMessage ?? "";
        }

        /// <inheritdoc/>
        protected TraceAssertException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            assertDetailedMessage = info.GetString("AssertDetailedMessage");
            assertMessage = info.GetString("AssertMessage");
        }

        /// <summary>
        /// Gets the original assert detailed message.
        /// </summary>
        public string AssertDetailedMessage
        {
            get { return assertDetailedMessage; }
        }

        /// <summary>
        /// Gets the original assert message.
        /// </summary>
        public string AssertMessage
        {
            get { return assertMessage; }
        }

        /// <summary>
        /// Gets a message that describes the current exception.
        /// </summary>
        public override string Message
        {
            get
            {
                string result = "Debug.Assert() Failure";

                if (!String.IsNullOrEmpty(AssertMessage))
                {
                    result += " : " + AssertMessage;

                    if (!String.IsNullOrEmpty(AssertDetailedMessage))
                    {
                        result += Environment.NewLine + "Detailed Message:" + Environment.NewLine + AssertDetailedMessage;
                    }
                }

                return result;
            }
        }

        /// <inheritdoc/>
        [SuppressMessage("NuGet.Client", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Protected with the Guard class")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("AssertDetailedMessage", assertDetailedMessage);
            info.AddValue("AssertMessage", assertMessage);

            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Swallows asserts
    /// </summary>
    public sealed class AssertHandler : TraceListener
    {
        private TraceListener defaultListener;
        private const string AssertHandlerName = "Foundation.Utility.AssertHandler";

        /// <summary>
        /// Eats Debug.Fail and failed asserts
        /// </summary>
        public AssertHandler()
            : base(AssertHandler.AssertHandlerName)
        {
            this.defaultListener = Debug.Listeners["Default"];
            if (this.defaultListener == null)
            {
                // Quite likely in an XUnit ExceptionAndOutputCaptureCommand, which has replaced the "normal" listeners
                foreach (TraceListener listener in Debug.Listeners)
                {
                    if (listener.GetType().FullName == "Foundation.Utility.AssertExceptionHandler")
                    {
                        this.defaultListener = listener;
                        break;
                    }
                }
            }

            if (this.defaultListener != null)
            {
                Debug.Listeners.Remove(this.defaultListener);
            }
            else if (Debug.Listeners[AssertHandler.AssertHandlerName] == null)
            {
                // This could potentially happen if we upgrade XUnit- look for the new class
                throw new InvalidOperationException("Could not find a default listener and we're not nested");
            }

            Debug.Listeners.Add(this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // Remove the listener before disposing the suppress token --
                // the listener may output warnings to the log which will reenter us.
                if (this.defaultListener != null)
                {
                    Debug.Listeners.Add(this.defaultListener);
                }
                Debug.Listeners.Remove(this);
            }
        }

        public override void Fail(string message)
        {
            // Do nothing
        }

        public override void Fail(string message, string detailMessage)
        {
            // Do nothing
        }

        public override void Write(string message)
        {
            // Do nothing
        }

        public override void WriteLine(string message)
        {
            // Do nothing
        }
    }
}
