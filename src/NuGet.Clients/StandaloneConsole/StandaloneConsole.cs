using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using NuGet.PackageManagement.UI;
using NuGetConsole;

namespace StandaloneConsole
{
    internal class StandaloneConsole : IConsole, IConsoleDispatcher
    {
        private static readonly Color[] _consoleColors;

        private readonly IntPtr _pKeybLayout = NativeMethods.GetKeyboardLayout(0);
        private readonly BlockingCollection<VsKeyInfo> _keyBuffer = new BlockingCollection<VsKeyInfo>();
        private CancellationTokenSource _cancelWaitKeySource;

        static StandaloneConsole()
        {
            // colors copied from hkcu:\Console color table
            _consoleColors = new Color[16]
            {
                Color.FromRgb(0x00, 0x00, 0x00),
                Color.FromRgb(0x00, 0x00, 0x80),
                Color.FromRgb(0x00, 0x80, 0x00),
                Color.FromRgb(0x00, 0x80, 0x80),
                Color.FromRgb(0x80, 0x00, 0x00),
                Color.FromRgb(0x80, 0x00, 0x80),
                Color.FromRgb(0x80, 0x80, 0x00),
                Color.FromRgb(0xC0, 0xC0, 0xC0),
                Color.FromRgb(0x80, 0x80, 0x80),
                Color.FromRgb(0x00, 0x00, 0xFF),
                Color.FromRgb(0x00, 0xFF, 0x00),
                Color.FromRgb(0x00, 0xFF, 0xFF),
                Color.FromRgb(0xFF, 0x00, 0x00),
                Color.FromRgb(0xFF, 0x00, 0xFF),
                Color.FromRgb(0xFF, 0xFF, 0x00),
                Color.FromRgb(0xFF, 0xFF, 0xFF)
            };
        }

        public int ConsoleWidth => Console.WindowWidth;

        public IConsoleDispatcher Dispatcher => this;

        public IHost Host { get; set; }

        public bool IsExecutingCommand => false;

        public bool IsExecutingReadKey { get; private set; }

        public bool IsKeyAvailable => _keyBuffer.Count > 0;

        public bool IsStartCompleted => true;

        public bool ShowDisclaimerHeader => true;

        public event EventHandler StartCompleted;
        public event EventHandler StartWaitingKey;

        public void AcceptKeyInput()
        {
        }

        public void Clear() => Console.Clear();

        public void ClearConsole() => Clear();

        public void Start()
        {
            if (StartCompleted != null)
            {
                StartCompleted(this, EventArgs.Empty);
            }
        }

        public VsKeyInfo WaitKey()
        {
            try
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ExecuteReadKey();
                });

                _cancelWaitKeySource = new CancellationTokenSource();
                IsExecutingReadKey = true;

                // blocking call
                var key = _keyBuffer.Take(_cancelWaitKeySource.Token);

                return key;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                IsExecutingReadKey = false;
            }
        }

        private void ExecuteReadKey()
        {
            var cki = Console.ReadKey(intercept: true);

            // catch current modifiers as early as possible
            var capsLockToggled = Keyboard.IsKeyToggled(Key.CapsLock);
            var numLockToggled = Keyboard.IsKeyToggled(Key.NumLock);

            // convert from char to virtual key, using current thread's input locale
            var keyScan = NativeMethods.VkKeyScanEx(cki.KeyChar, _pKeybLayout);

            // virtual key is in LSB, shiftstate in MSB.
            var virtualKey = (byte)(keyScan & 0x00ff);

            // convert from virtual key to wpf key.
            var key = KeyInterop.KeyFromVirtualKey(virtualKey);

            // create nugetconsole.vskeyinfo to marshal info to 
            var keyInfo = VsKeyInfo.Create(
                key: key,
                keyChar: cki.KeyChar,
                virtualKey: virtualKey,
                keyStates: KeyStates.Down,
                shiftPressed: cki.Modifiers.HasFlag(ConsoleModifiers.Shift),
                altPressed: cki.Modifiers.HasFlag(ConsoleModifiers.Alt),
                controlPressed: cki.Modifiers.HasFlag(ConsoleModifiers.Control),
                capsLockToggled: capsLockToggled,
                numLockToggled: numLockToggled);

            _keyBuffer.Add(keyInfo);
        }

        public void Write(string text) => Console.Write(text);

        public void Write(string text, Color? foreground, Color? background)
        {
            var savefc = Console.ForegroundColor;
            var savebc = Console.BackgroundColor;

            var writefc = ToColor(foreground);
            if (writefc.HasValue)
            {
                Console.ForegroundColor = writefc.Value;
            }

            var writebc = ToColor(background);
            if (writebc.HasValue)
            {
                Console.BackgroundColor = writebc.Value;
            }

            Console.Write(text);

            Console.ForegroundColor = savefc;
            Console.BackgroundColor = savebc;
        }

        public void WriteBackspace() => Console.Write("\b");

        public void WriteLine(string text) => Console.WriteLine(text);

        public void WriteProgress(string currentOperation, int percentComplete)
        {
        }

        private ConsoleColor? ToColor(Color? color)
        {
            if (color.HasValue)
            {
                var index = Array.FindIndex(_consoleColors, c => c == color);
                if (Enum.IsDefined(typeof(ConsoleColor), index))
                {
                    return (ConsoleColor)index;
                }
            }

            return null; // invalid color
        }
    }
}
