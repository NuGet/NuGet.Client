using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NuGet.Tests.Foundation.Utility
{
    /// <summary>
    /// A class to execute a method on the UI thread. This must be constructed on the UI thread,
    /// usually by calling UIThreadDispatcher.InitializeDispatcher().  If you need a result from
    /// an asynchronous call (BeginInvoke) make sure to use the overloads that take a Func rather
    /// than an Action.
    /// </summary>
    /// <example>
    /// UIThreadDispatcher.Instance.BeginInvoke(DispatcherPriority.Normal, () => { \\ my code });
    /// UIThreadDispatcher.Instance.BeginInvoke(DispatcherPriority.Normal, this.OnIdle);  // if OnIdle is of the form void OnIdle()
    /// </example>
    public sealed class UIThreadDispatcher
    {
        // fields
        private Dispatcher uiThreadDispatcher;

        #region Singleton Management

        static UIThreadDispatcher dispatcher;

        public static void InitializeInstance()
        {
            // Note: we do initialize this more than once
            // in OOP scenarios.  Technically we could create
            // a new instance because this class is stateless but
            // this code makes it clearer that we're creating a
            // singleton.
            if (UIThreadDispatcher.dispatcher == null)
            {
                UIThreadDispatcher.dispatcher = new UIThreadDispatcher();
            }

            // If we already had one, make sure if we redundantly initialize we
            // did it on the same thread.
            Debug.Assert(UIThreadDispatcher.dispatcher.IsUIThread, "InitializeInstance should always be called from UI thread");
        }

        public static bool IsInitialized
        {
            get { return UIThreadDispatcher.dispatcher != null; }
        }

        public static UIThreadDispatcher Instance
        {
            get
            {
                Debug.Assert(dispatcher != null, "Instance getter called before Instance is initialized");

                return dispatcher;
            }
            set
            {
                dispatcher = value;
            }
        }


#if TESTFOUNDATION
        /// <summary>
        /// DO NOT USE THIS.
        /// Unless you're dealing with Unit Tests Setup/Teardown.
        /// In order to run Unit tests we have to constanly reboot UIThreadDispatcher, this behavior is not supported outside UnitTest world.
        /// </summary>

        public static void ResetDispatcher()

        {
            Debug.Assert(UnitTestHelper.IsUnitTestEnvironment, "This should be used only for unit tests.");
            dispatcher = null;
        }

        /// <summary>
        /// DO NOT USE THIS.
        /// Only for unit test setup/teardown.
        /// </summary>
        public bool IsDispatcher(Dispatcher dispatcher)
        {
            Debug.Assert(UnitTestHelper.IsUnitTestEnvironment, "This should be used only for unit tests.");
            return dispatcher == this.uiThreadDispatcher;
        }
#endif

        #endregion Singleton Management

        public UIThreadDispatcher()
        {
            this.uiThreadDispatcher = Dispatcher.CurrentDispatcher;
        }

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Action action)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return this.uiThreadDispatcher.BeginInvoke(priority, action);
            }
            return null;
        }

        public DispatcherOperation BeginInvoke<TResult>(DispatcherPriority priority, Func<TResult> func)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return this.uiThreadDispatcher.BeginInvoke(priority, func);
            }
            return null;
        }

        public DispatcherOperation BeginInvoke<T>(DispatcherPriority priority, Action<T> action, T arg)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return this.uiThreadDispatcher.BeginInvoke(priority, action, arg);
            }
            return null;
        }

        public DispatcherOperation BeginInvoke<T, TResult>(DispatcherPriority priority, Func<T, TResult> func, T arg)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return this.uiThreadDispatcher.BeginInvoke(priority, func, arg);
            }
            return null;
        }

        public DispatcherOperation BeginInvoke<T1, T2>(DispatcherPriority priority, Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return this.uiThreadDispatcher.BeginInvoke(priority, action, arg1, arg2);
            }
            return null;
        }

        public void Invoke(DispatcherPriority priority, Action action)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                this.uiThreadDispatcher.Invoke(priority, action);
            }
        }

        /// <summary>
        /// Begin the specified action after the specified delay.
        /// </summary>
        public DispatcherOperation BeginInvokeAfter(TimeSpan delay, DispatcherPriority priority, Action action)
        {
            DispatcherOperation operation = this.BeginInvoke(DispatcherPriority.Inactive, action);
            DispatcherTimer timer = null;
            timer = new DispatcherTimer(
                interval: delay,
                priority: priority,
                callback: (sender, e) =>
                {
                    timer.Stop();
                    if (operation.Status == DispatcherOperationStatus.Pending)
                    {
                        operation.Priority = priority;
                    }
                },
                dispatcher: this.uiThreadDispatcher);

            return operation;
        }


        /// <summary>
        /// Invokes the specified action after the specified delay.
        /// </summary>
        public void InvokeAfter(TimeSpan delay, DispatcherPriority priority, Action action)
        {
            DispatcherTimer timer = null;
            timer = new DispatcherTimer(
                interval: delay,
                priority: priority,
                callback: (sender, e) =>
                {
                    timer.Stop();
                    action();
                },
                dispatcher: this.uiThreadDispatcher);
        }

        public void Invoke(Action action)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                this.uiThreadDispatcher.Invoke(DispatcherPriority.Normal, action);
            }
        }

        public void Invoke<T>(DispatcherPriority priority, Action<T> action, T arg)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                this.uiThreadDispatcher.Invoke(priority, action, arg);
            }
        }

        public void Invoke<T1, T2>(DispatcherPriority priority, Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                this.uiThreadDispatcher.Invoke(priority, action, arg1, arg2);
            }
        }

        public TResult Invoke<TResult>(DispatcherPriority priority, Func<TResult> func)
        {
            if (!this.uiThreadDispatcher.HasShutdownStarted)
            {
                return (TResult)this.uiThreadDispatcher.Invoke(priority, func);
            }
            return default(TResult);
        }

        public DispatcherHooks Hooks
        {
            get
            {
                return this.uiThreadDispatcher == null ? null : this.uiThreadDispatcher.Hooks;
            }
        }

        /// <summary>
        /// Clears all of the pending execution frames from the UI dispatcher.
        /// </summary>
        public void DoEvents()
        {
            (new DispatcherHelper()).ClearFrames(this.uiThreadDispatcher);
        }

        public bool IsUIThread
        {
            get
            {
                return this.uiThreadDispatcher != null && this.uiThreadDispatcher.Thread == Thread.CurrentThread;
            }
        }

        public bool IsUIThreadRunning
        {
            get
            {
                return (this.uiThreadDispatcher.Thread.ThreadState == System.Threading.ThreadState.Running);
            }
        }
    }
}
