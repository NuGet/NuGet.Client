using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : INuGetProjectContext
    {
        private readonly Dispatcher _uiDispatcher;
        private DateTime _loadedTime;
        private readonly TimeSpan minimumVisibleTime = TimeSpan.FromMilliseconds(500);

        public ProgressDialog()
        {
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            this.Loaded += ProgressDialog_Loaded;
            InitializeComponent();
        }

        void ProgressDialog_Loaded(object sender, RoutedEventArgs e)
        {
            _loadedTime = DateTime.UtcNow;
        }

        public FileConflictAction FileConflictAction
        {
            get;
            set;
        }

        public void CloseWindow()
        {
            TimeSpan timeOpened = DateTime.UtcNow - _loadedTime;
            if (timeOpened < minimumVisibleTime)
            {
                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(minimumVisibleTime - timeOpened);
                    Dispatcher.Invoke(
                        () => this.Close());
                });
            }
            else
            {
                this.Close();
            }
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            var s = string.Format(CultureInfo.CurrentCulture, message, args);

            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.BeginInvoke(
                    new Action<MessageLevel, string>(AddMessage),
                    level,
                    s);
            }
            else
            {
                AddMessage(level, s);
            }
        }

        private void AddMessage(MessageLevel level, string message)
        {
            // for the dialog we ignore debug messages
            if (level != MessageLevel.Debug)
            {
                AddMessageToDialog(level, message);
            }
        }

        private void AddMessageToDialog(MessageLevel level, string message)
        {
            Brush messageBrush;

            // select message color based on MessageLevel value.
            // these colors match the colors in the console, which are set in MyHostUI.cs
            if (SystemParameters.HighContrast)
            {
                // Use the plain System brush
                messageBrush = SystemColors.ControlTextBrush;
            }
            else
            {
                switch (level)
                {
                    case MessageLevel.Debug:
                        messageBrush = System.Windows.Media.Brushes.DarkGray;
                        break;

                    case MessageLevel.Error:
                        messageBrush = System.Windows.Media.Brushes.Red;
                        break;

                    case MessageLevel.Warning:
                        messageBrush = System.Windows.Media.Brushes.Magenta;
                        break;

                    default:
                        messageBrush = System.Windows.Media.Brushes.Black;
                        break;
                }
            }

            Paragraph paragraph = null;

            // delay creating the FlowDocument for the RichTextBox
            // the FlowDocument will contain a single Paragraph, which
            // contains all the logging messages.
            if (MessagePane.Document == null)
            {
                MessagePane.Document = new FlowDocument();
                paragraph = new Paragraph();
                MessagePane.Document.Blocks.Add(paragraph);
            }
            else
            {
                // if the FlowDocument has been created before, retrieve 
                // the last paragraph from it.
                paragraph = (Paragraph)MessagePane.Document.Blocks.LastBlock;
            }

            // each message is represented by a Run element
            var run = new Run(message)
            {
                Foreground = messageBrush
            };

            // if the paragraph is non-empty, add a line break before the new message
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            paragraph.Inlines.Add(run);

            // scroll to the end to show the latest message
            MessagePane.ScrollToEnd();
        }

        private FileConflictAction ShowFileConflictResolution(string message)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                object result = _uiDispatcher.Invoke(
                    new Func<string, FileConflictAction>(ShowFileConflictResolution),
                    message);
                return (FileConflictAction)result;
            }

            var fileConflictDialog = new FileConflictDialog()
            {
                Question = message
            };

            if (fileConflictDialog.ShowModal() == true)
            {
                return fileConflictDialog.UserSelection;
            }
            else
            {
                return FileConflictAction.IgnoreAll;
            }
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            if (FileConflictAction == FileConflictAction.PromptUser)
            {
                var resolution = ShowFileConflictResolution(message);

                if (resolution == FileConflictAction.IgnoreAll ||
                    resolution == FileConflictAction.OverwriteAll)
                {
                    FileConflictAction = resolution;
                }
                return resolution;
            }

            return FileConflictAction;
        }
    }
}
