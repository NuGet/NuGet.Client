using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NuGet.Tests.Foundation.Utility.Assemblies;

namespace NuGet.Tests.Foundation.Utility.Diagnostics
{
    public static class DebugHelper
    {
#if DEBUG
        private static int inAssert;
#endif

        [Conditional("DEBUG")]
        public static void Assert(bool condition, string message = null, string stackTrace = null)
        {
#if DEBUG
            if (!condition)
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        [Conditional("DEBUG")]
        public static void Fail(string message, string stackTrace = null)
        {
            DebugHelper.InternalFail(message, false, stackTrace);
        }

        [Conditional("DEBUG")]
        public static void AssertOnce(bool condition, string message = null, string stackTrace = null)
        {
#if DEBUG
            if (!condition)
            {
                DebugHelper.InternalFail(message, true, stackTrace);
            }
#endif
        }

        [Conditional("DEBUG")]
        public static void FailOnce(string message, string stackTrace = null)
        {
            DebugHelper.InternalFail(message, true, stackTrace);
        }


        /// <summary>
        /// Use to verify conditions in finalizers.
        /// </summary>
        [Conditional("DEBUG")]
        public static void FinalizerAssert(bool condition, string message = null, string stackTrace = null)
        {
            if (!UnitTestHelper.IsUnitTestEnvironment)
            {
                DebugHelper.Assert(condition, message, stackTrace);
            }
        }

        /// <summary>
        /// Use to verify conditions in finalizers.
        /// </summary>
        [Conditional("DEBUG")]
        public static void FinalizerAssertOnce(bool condition, string message = null, string stackTrace = null)
        {
            if (!UnitTestHelper.IsUnitTestEnvironment)
            {
                DebugHelper.AssertOnce(condition, message, stackTrace);
            }
        }

        /// <summary>
        /// Use to verify conditions in finalizers.
        /// </summary>
        [Conditional("DEBUG")]
        public static void FinalizerFail(string message = null, string stackTrace = null)
        {
            if (!UnitTestHelper.IsUnitTestEnvironment)
            {
                DebugHelper.Fail(message, stackTrace);
            }
        }

        /// <summary>
        /// Use to verify conditions in finalizers.
        /// </summary>
        [Conditional("DEBUG")]
        public static void FinalizerFailOnce(string message = null, string stackTrace = null)
        {
            if (!UnitTestHelper.IsUnitTestEnvironment)
            {
                DebugHelper.FailOnce(message, stackTrace);
            }
        }

        /// <summary>
        /// Used to validate that the given value is non-null.
        /// </summary>
        /// <typeparam name="T">The type of the value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertValue<T>(T value, string message = null, string stackTrace = null)
            where T : class
        {
#if DEBUG
            if (Object.ReferenceEquals(value, null))
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to enforce an "any value" contract. This is useful for
        /// code maintenance, as it gives an instant visual indication to the developer
        /// about the fact that the argument can have any possible value, including null.
        /// This becomes a no-op in Release builds.
        /// </summary>
        /// <typeparam name="T">The type of the value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertAnyValue<T>(T value)
            where T : class
        {
        }

        /// <summary>
        /// Used to validate that a string is non-null and non-empty.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertNonempty(string value, string message = null, string stackTrace = null)
        {
#if DEBUG
            if (String.IsNullOrEmpty(value))
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to validate that all the strings in a list are non-null and non-empty.
        /// </summary>
        /// <param name="list">The list to test.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertAllNonempty(IList<string> list, string message = null, string stackTrace = null)
        {
#if DEBUG
            if (Object.ReferenceEquals(list, null))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (String.IsNullOrEmpty(list[i]))
                    DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to validate that a call was made by a specific member of a specific type
        /// </summary>
        /// <param name="type">The type of the caller</param>
        /// <param name="methodName">The method that made a call</param>
        /// <param name="bindingAttributes">Binding flags to search a member</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertCaller(Type type, string memberName,
            BindingFlags bindingAttributes = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            string message = null, string stackTrace = null)
        {
#if DEBUG
            StackFrame stackFrame = new StackTrace(fNeedFileInfo: false).GetFrame(2);
            if (stackFrame == null || !object.Equals(stackFrame.GetMethod(), type.GetMethod(memberName, bindingAttributes)))
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to validate that a collection is non-null and non-empty.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="collection">The collection being tested.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertNonempty<T>(ICollection<T> collection, string message = null, string stackTrace = null)
        {
#if DEBUG
            if (Object.ReferenceEquals(collection, null) || collection.Count == 0)
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to validate that all the items in a list are non-null if the list is non-null.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="list">The list to test.</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertAllValues<T>(IList<T> list, string message = null, string stackTrace = null)
            where T : class
        {
#if DEBUG
            if (Object.ReferenceEquals(list, null))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (Object.ReferenceEquals(list[i], null))
                    DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Used to validate that all the items in first collection are equal to items in second collection. Respects order.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="c1">First collection</param>
        /// <param name="c2">Second collection</param>
        /// <param name="message">An optional message to emit if the assert fails.</param>
        /// <param name="stackTrace">An optional stack trace to show if the assert fails.</param>
        [Conditional("DEBUG")]
        public static void AssertAllValuesAreEqual<T>(IEnumerable<T> c1, IEnumerable<T> c2, Func<T, T, bool> comparison = null, string message = null, string stackTrace = null)
            where T : class
        {
#if DEBUG
            if (Object.ReferenceEquals(c1, c2))
            {
                return;
            }

            if (Object.ReferenceEquals(c1, null) || Object.ReferenceEquals(c2, null))
            {
                DebugHelper.InternalFail(message, false, stackTrace);
                return;
            }

            comparison = comparison ?? Object.ReferenceEquals;
            IEnumerator<T> e1 = c1.GetEnumerator();
            IEnumerator<T> e2 = c2.GetEnumerator();

            while (e1.MoveNext())
            {
                if (!e2.MoveNext())
                {
                    DebugHelper.InternalFail(message, false, stackTrace);
                }

                if (!comparison(e1.Current, e2.Current))
                {
                    DebugHelper.InternalFail(message, false, stackTrace);
                }
            }

            if (e2.MoveNext())
            {
                DebugHelper.InternalFail(message, false, stackTrace);
            }
#endif
        }

        /// <summary>
        /// Returns true if an assert is currently being displayed.
        /// </summary>
#if DEBUG
        public static bool InAssert { get { return DebugHelper.inAssert > 0; } }
#endif

        #region Private implementations
#if !DEBUG
        [Conditional("DEBUG")]
        private static void InternalFail(string message, bool once, string stackTrace = null)
        {
        }
#else
        private static Dictionary<string, int> stackTraceCount = new Dictionary<string, int>();

        // Disable debugger break by editing the value of this field in debugger.
        private static bool debuggerBreakEnabled = true;

        private static bool AssertUIEnabled
        {
            get
            {
                foreach (object listener in Trace.Listeners)
                {
                    DefaultTraceListener defaultListener = listener as DefaultTraceListener;
                    if (defaultListener != null && defaultListener.AssertUiEnabled)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Similar to the assert dialog in System.Diagnostics except this dialog can take stack trace
        /// so that it can display original thread stack track when dialog is shown in seperate thread.
        /// </summary>
        /// <remarks>
        /// Displaying assert dialog can cause re-entrance or use of uninitialized data.
        /// This frequently happens in rendering pipeline or input event handling etc.
        /// This method will block the current thread and display dialog in a seperate thread.
        /// Therefore it will avoid re-entrance, and allow you to attach debugger and investigate
        /// exact state when the assert happens.
        /// </remarks>
        private static void InternalFail(string message, bool once, string stackTrace = null)
        {
            if (stackTrace == null)
            {
                // Capture the stack trace on original thread that raises the assert.
                stackTrace = new StackTrace(skipFrames: 2, fNeedFileInfo: true).ToString();
            }

            if (once)
            {
                // Assert with same stack will only show the dialog once.
                // Later ones will be printed to debugger output console.
                int count;
                if (stackTraceCount.TryGetValue(stackTrace, out count))
                {
                    stackTraceCount[stackTrace]++;
                    Debug.WriteLine("Assert failure #{0}: {1}\n{2}", count, message, stackTrace);
                    return;
                }
                else
                {
                    stackTraceCount[stackTrace] = 1;
                }
            }

            message += string.Format("\nProcess: {0} (0x{1:X}), Thread: {2} (0x{3:X})\n",
                Process.GetCurrentProcess().ProcessName,
                Process.GetCurrentProcess().Id,
                Thread.CurrentThread.Name,
                DebugHelper.GetCurrentThreadId());

            if (Debugger.IsAttached)
            {
                if (DebugHelper.debuggerBreakEnabled)
                {
                    Debugger.Break();
                }
            }
            else if (!DebugHelper.AssertUIEnabled
                // Should never push UI in unit tests
                || UnitTestHelper.IsUnitTestEnvironment
                )
            {
                Debug.Fail(message);
            }
            else
            {
                if (UIThreadDispatcher.IsInitialized &&
                    UIThreadDispatcher.Instance.IsUIThreadRunning &&
                    !UIThreadDispatcher.Instance.IsUIThread)
                {
                    UIThreadDispatcher.Instance.Invoke(DispatcherPriority.Send, () =>
                    {
                        DisplayDebugMessageDialogAndBlock(message, stackTrace);
                    });
                }
                else
                {
                    DisplayDebugMessageDialogAndBlock(message, stackTrace);
                }
            }
        }

        private static void DisplayDebugMessageDialogAndBlock(string message, string stackTrace)
        {
            if (UnitTestHelper.IsUnitTestEnvironment)
            {
                throw new InvalidOperationException("Cannot push dialogs during unit tests.");
            }

            Interlocked.Increment(ref DebugHelper.inAssert);
            try
            {
                using (Task task = Task.Run(() => DebugHelper.DisplayDebugMessageDialog(message, stackTrace)))
                {
                    task.Wait();
                }
            }
            finally
            {
                Interlocked.Decrement(ref DebugHelper.inAssert);
            }
        }

        private static void DisplayDebugMessageDialog(string message, string stackTrace)
        {
            if (UnitTestHelper.IsUnitTestEnvironment)
            {
                throw new InvalidOperationException("Cannot push dialogs during unit tests.");
            }

            message = FitMessageToScreen(message + "\n" + stackTrace);

            uint type = Interop.UnsafeNativeMethods.MB_ICONERROR |
                Interop.UnsafeNativeMethods.MB_TASKMODAL |
                Interop.UnsafeNativeMethods.MB_ABORTRETRYIGNORE;
            string caption = "Abort=Exit, Retry=Debug, Ignore=Continue";

            IntPtr ownerWindow = IntPtr.Zero;
            Process process = Process.GetCurrentProcess();
            if (process != null)
            {
                ownerWindow = process.MainWindowHandle;
            }

            int result = Interop.UnsafeNativeMethods.MessageBox(ownerWindow, message, caption, type);
            switch (result)
            {
                case Interop.UnsafeNativeMethods.IDABORT:
                    Environment.Exit(1);
                    break;

                case Interop.UnsafeNativeMethods.IDRETRY:
                    if (!Debugger.IsAttached)
                    {
                        Debugger.Launch();
                    }

                    // If you debug break at this line, please
                    // switch to UI thread to investigate the assert.
                    Debugger.Break();
                    break;
            }
        }

        private static string FitMessageToScreen(string stackTrace)
        {
            // A very rough estimate to fit message in screen.
            int maxLineNumber = (int)(SystemParameters.PrimaryScreenHeight / SystemParameters.CaptionHeight);

            string[] lines = stackTrace.Split('\n');
            if (lines.Length > maxLineNumber)
            {
                stackTrace = lines.Take(maxLineNumber).Aggregate((a, b) => a + b + "\n");
                stackTrace += "\n...";
            }

            return stackTrace;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
#endif
        #endregion
    }
}
