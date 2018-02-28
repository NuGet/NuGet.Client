using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NuGet.Tests.Foundation.TestAttributes;
using NuGet.Tests.Foundation.TestCommands;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.Utility
{
    public class TestUIThreadHelper
    {
        private static TestUIThreadHelper instance;
        private static bool ReuseCurrentApplication;
        private ConcurrentStack<Action> teardownActions = new ConcurrentStack<Action>();
        private Thread staThread;
        private Application mainApplication;
        private long initializationComplete;
        private static object initializationLock = new object();
        private DispatcherFrame runFrame;
        private int unmanagedThreadId;

        private TestUIThreadHelper()
        {
            AppDomain.CurrentDomain.DomainUnload += this.DomainTeardown;
            AppDomain.CurrentDomain.ProcessExit += this.DomainTeardown;

            //We want to maintain an application on a single STA thread
            //set to Background so that it won't block process exit.
            Thread thread = new Thread(this.CreateAndRunApplication);
            thread.Name = "Test UI Runner Thread";
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join(50);

            while (Interlocked.Read(ref this.initializationComplete) < 1)
            {
                Thread.Sleep(10);
            }

            this.staThread = thread;
        }

        public static bool ReuseCurrentApp
        {
            get
            {
                return TestUIThreadHelper.ReuseCurrentApplication;
            }
            set
            {
                if (TestUIThreadHelper.instance != null)
                {
                    throw new InvalidOperationException("Need to set this property before the instance is retreived");
                }

                TestUIThreadHelper.ReuseCurrentApplication = value;
            }
        }

        public static TestUIThreadHelper Instance
        {
            get
            {
                lock (TestUIThreadHelper.initializationLock)
                {
                    if (TestUIThreadHelper.instance == null)
                    {
                        TestUIThreadHelper.instance = new TestUIThreadHelper();
                    }

                    return TestUIThreadHelper.instance;
                }
            }
        }

        private void CreateAndRunApplication()
        {
            if (!TestUIThreadHelper.ReuseCurrentApplication)
            {
                if (Application.Current != null)
                {
                    // Need to be on our own sta thread
                    Debug.WriteLine("Already had an application!");
                    Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                }

                if (Application.Current != null)
                {
                    throw new InvalidOperationException("Unable to shut down existing application object.");
                }
            }

            // Kick OLE so we can use the clipboard if necessary
            NativeMethods.OleInitialize();

            this.unmanagedThreadId = NativeMethods.GetCurrentThreadId();
            Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;
            Debug.WriteLine("CurrentDispatcher thread {0}", currentDispatcher.Thread.ManagedThreadId);

            if (TestUIThreadHelper.ReuseCurrentApplication)
            {
                this.mainApplication = Application.Current;
            }
            else
            {
                // Creating a new Application sets the current in the constructor.
                this.mainApplication = new Application();

                // If ShutdownMode is set to OnLastWindowClose (default) or OnMainWindowClose we'll lose the
                // application if any test directly or indirectly creates and destroys a Window.
                //
                // One notable case is where we close a HostProject, which disposes ProjectContext, which eventually
                // flushes the ArtboardOverlayWindow, which UpdatesWindowListsOnClose(), which sees there are no
                // more windows and calls Application.CriticalShutdown on the dispatcher.
                //
                // As the ShutdownMode is public we can put a Window in to try and avoid scenarios where test
                // code inadvertently changes it. For now we'll rely on ShutdownMode.
                this.mainApplication.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            // Application/Dispatcher.Run ultimately becomes: PushFrame(new DispatcherFrame());
            // We want to manage things ourselves, pushing our own frame to do so
            this.runFrame = new DispatcherFrame(exitWhenRequested: false);

            List<ExceptionDispatchInfo> caughtExceptions = new List<ExceptionDispatchInfo>();

            // We're ready to be referenced
            Interlocked.Increment(ref this.initializationComplete);
            while (this.runFrame.Continue)
            {
                ExceptionDispatchInfo exception = TestUIThreadHelper.SafeInvoke(() => Dispatcher.PushFrame(this.runFrame));

                if (exception != null)
                {
                    caughtExceptions.Add(exception);
                }
            }

            if (caughtExceptions.Count > 0)
            {
                Console.WriteLine("Threw one or more exceptions during the AppDomain run.");
                throw new AggregateException(caughtExceptions.Select(ce => ce.SourceException).ToArray());
            }
        }

        private void DomainTeardown(object sender, EventArgs e)
        {
            // This event can fire on ANY thread. Including the GC Finalizer thread. If teardown code
            // calls GC.WaitForPendingFinalizers() on cleanup we will deadlock in that case.
            AppDomain.CurrentDomain.DomainUnload -= this.DomainTeardown;
            AppDomain.CurrentDomain.ProcessExit -= this.DomainTeardown;

            List<ExceptionDispatchInfo> caughtExceptions = new List<ExceptionDispatchInfo>();
            Action teardownAction;
            while (this.teardownActions.TryPop(out teardownAction))
            {
                using (new AssertHandler())
                {
                    ExceptionDispatchInfo exception = TestUIThreadHelper.SafeInvoke(teardownAction);
                    if (exception != null)
                    {
                        caughtExceptions.Add(exception);
                    }
                }
            }

            Thread thread = this.staThread;
            this.staThread = null;

            Debug.WriteLine("Current thread {0}, dispatcher thread {1}", Thread.CurrentThread.ManagedThreadId, thread.ManagedThreadId);

            Application application = this.mainApplication;
            this.mainApplication = null;
            Dispatcher dispatcher = application.Dispatcher;
            Debug.WriteLine("Dispatcher is for thread {0}", dispatcher.Thread.ManagedThreadId);
            Debug.WriteLine("Dispatcher shutdown started: {0}", dispatcher.HasShutdownStarted);
            Debug.WriteLine("Dispatcher shutdown completed: {0}", dispatcher.HasShutdownFinished);

            // After we allow our holding frame to continue there may or may not be more messages pending for the dispatcher.
            // If there are no messages there is a good chance the dispatcher will hang. Posting a message to the dispatcher's
            // thread will allow it to see that the frame can exit and the dispatcher will dispose itself.
            Debug.WriteLine("Attempting to continue frame");
            this.runFrame.Continue = false;
            Debug.WriteLine("Attempting to post a thread message to get the dispatcher to pump.");
            NativeMethods.PostThreadMessage(this.unmanagedThreadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);

            // If the thread is still alive, allow it to exit normally so the dispatcher can continue to clear pending work items
            if (thread.IsAlive)
            {
                Debug.WriteLine("Thread is alive, trying to wait.");
            }

            if (caughtExceptions.Count == 1 &&
                caughtExceptions[0].SourceException.GetType().FullName != "Microsoft.Test.Apex.Interop.MessageFilterRegistrationException")
            {
                caughtExceptions[0].Throw();
            }
            else if (caughtExceptions.Count > 1)
            {
                throw new AggregateException("Multiple exceptions thrown during app domain teardown.", caughtExceptions.Select(ce => ce.SourceException).ToArray());
            }
        }

        /// <summary>
        /// Get the dispatcher for the "main" UI thread
        /// </summary>
        public Dispatcher UIThreadDispatcher
        {
            get { return this.mainApplication.Dispatcher; }
        }

        public static bool ShouldRunOnTestUIThread(object value)
        {
            ReflectionTypeInfo reflectionTypeInfo = value as ReflectionTypeInfo;
            if (reflectionTypeInfo != null && reflectionTypeInfo.GetCustomAttributes(typeof(RunOnTestUIThreadAttribute)).Any())
            {
                return true;
            }
            else if (value.GetAttributes<RunOnTestUIThreadAttribute>(inherit: true).Any()
                || value.GetAttributesFromGenericTypeArguments<RunOnTestUIThreadAttribute>(inherit: true).Any())
            {
                return true;
            }

            return false;
        }

        public static bool ShouldRunOnSTAThread(object value)
        {
            ReflectionTypeInfo reflectionTypeInfo = value as ReflectionTypeInfo;
            if (reflectionTypeInfo != null && reflectionTypeInfo.GetCustomAttributes(typeof(RunOnSTAThreadAttribute)).Any())
            {
                return true;
            }
            else if (value.GetAttributes<RunOnSTAThreadAttribute>(inherit: true).Any()
                || value.GetAttributesFromGenericTypeArguments<RunOnSTAThreadAttribute>(inherit: true).Any())
            {
                return true;
            }

            return false;
        }

        public static bool ShouldRunOnTestUIThread(Type type)
        {
            if (type.GetCustomAttributes<RunOnTestUIThreadAttribute>(inherit: true).Any())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Helper to catch exceptions and wrap them for aggregation or rethrow
        /// </summary>
        private static ExceptionDispatchInfo SafeInvoke(Action action)
        {
            ExceptionDispatchInfo exception = null;

            try
            {
                action();
            }
            catch (ThreadAbortException tae)
            {
                // If we don't stop a thread abort here we'll lose the ability to stop the dispatcher as it will never
                // know the thread is gone. We'll rethrow this as a nested failure to get normal reporting.
                Thread.ResetAbort();
                exception = ExceptionDispatchInfo.Capture(new Exception("Thread abort during test execution", tae));
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }

            return exception;
        }

        public void InvokeOnTestUIThread(Action action)
        {
            ExceptionDispatchInfo exception = null;

            if (Thread.CurrentThread == this.staThread)
            {
                // Already there, just invoke
                exception = TestUIThreadHelper.SafeInvoke(action);
            }
            else
            {
                // Don't invert the order and SafeInvoke the call to the dispatcher, it will deadlock if an exception is thrown
                exception = this.UIThreadDispatcher.Invoke(() => TestUIThreadHelper.SafeInvoke(action));
            }

            if (exception != null)
            {
                // We'll rethrow on the main thread to make sure we don't wreck the runner thread. The outer nested xunit
                // ExceptionAndOutputCaptureCommand will report the error.
                exception.Throw();
            }
        }

        public T InvokeOnTestUIThread<T>(Func<T> action)
        {
            T returnValue = default(T);
            Action wrappedAction = () => returnValue = action();
            this.InvokeOnTestUIThread(wrappedAction);
            return returnValue;
        }

        public Task<T> InvokeOnTestUIThread<T>(Func<Task<T>> action)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            Task<T> task;
            if (Thread.CurrentThread == this.staThread)
            {
                task = Task.FromResult(action().Result);
            }
            else
            {
                task = this.UIThreadDispatcher.Invoke(async delegate
                {
                    T defaultValue = default(T);
                    try
                    {
                        defaultValue = await action();
                    }
                    catch (ThreadAbortException)
                    {
                        Thread.ResetAbort();
                    }
                    return defaultValue;
                });
            }
            task.Wait();
            CopyTaskResultFrom<T>(tcs, task);
            return tcs.Task;
        }

        internal static void CopyTaskResultFrom<T>(TaskCompletionSource<T> tcs, Task<T> template)
        {
            if (tcs == null)
            {
                throw new ArgumentNullException("tcs");
            }
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }
            if (!template.IsCompleted)
            {
                throw new ArgumentException("Task must be completed first.", "template");
            }

            if (template.IsFaulted)
            {
                tcs.SetException(template.Exception);
            }
            else if (template.IsCanceled)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(template.Result);
            }
        }

        /// <summary>
        /// Register an action to run before the current appdomain unloads
        /// </summary>
        public void AddTeardownAction(Action teardownAction, bool performActionOnOriginalThread = true)
        {
            if (teardownAction == null) { throw new ArgumentNullException("teardownAction"); }

            if (!performActionOnOriginalThread)
            {
                this.teardownActions.Push(teardownAction);
                return;
            }

            Thread originalThread = Thread.CurrentThread;

            this.teardownActions.Push(() =>
            {
                if (originalThread == null || !originalThread.IsAlive)
                {
                    throw new InvalidOperationException("Originating thread for shutdown action has been terminated");
                }

                var threadDispatcher = Dispatcher.FromThread(originalThread);
                if (threadDispatcher == null || threadDispatcher.HasShutdownStarted || threadDispatcher.HasShutdownFinished)
                {
                    throw new InvalidOperationException("The dispatcher for the originating thread has shut down");
                }

                threadDispatcher.Invoke(teardownAction);
            });
        }
    }
}
