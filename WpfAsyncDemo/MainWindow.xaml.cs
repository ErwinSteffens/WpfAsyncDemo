using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Nito.AsyncEx;
using PropertyChanged;

namespace WpfAsyncDemo
{
    public partial class MainWindow
    {
        [ImplementPropertyChanged]
        protected class DataModel : IDisposable
        {
            private readonly CancellationTokenSource initializationCancelToken = new CancellationTokenSource();
            private bool isDisposed;

            private DelegateCommand cancelInitCommand;
            private AsyncCommand<string> reverseAsyncCommand;

            public ICommand ReverseAsyncCommand => this.reverseAsyncCommand ?? (this.reverseAsyncCommand = new AsyncCommand<string>(this.OnTestAsyncCommandHandler, this.OnCanTestAsyncCommandHandler));
            public ICommand CancelInitCommand => this.cancelInitCommand ?? (this.cancelInitCommand = new DelegateCommand(this.OnCancelInitCommand));

            public string Input { get; set; }
            public TimeSpan Delay { get; set; }
            public string Output { get; private set; }
            public INotifyTaskCompletion Initialization { get; set; }

            public DataModel()
            {
                this.Input = "Test string";
                this.Delay = TimeSpan.FromSeconds(5);

                this.Initialization = NotifyTaskCompletion.Create(this.InitializeAsync(this.initializationCancelToken.Token));
                this.Initialization.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(INotifyTaskCompletion.IsCompleted))
                    {
                        CommandManager.InvalidateRequerySuggested();
                    }
                };
            }

            public void Dispose()
            {
                if (!this.isDisposed)
                {
                    // Cancel operation
                    this.initializationCancelToken.Cancel();

                    // We're disposed
                    this.isDisposed = true;
                }
            }

            private async Task InitializeAsync(CancellationToken cancellationToken)
            {
                // Do some actions, when the cancellation token is set
                // (you could also cancel HTTP requests or other thing from here)
                cancellationToken.Register(() => Debug.WriteLine("Cancellation token is set."));

                try
                {
                    // This command will keep running, until it is being canceled
                    var i = 0;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Write a debug line every second
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false); // We don't do any GUI tasks

                        // Print to debug
                        Debug.WriteLine($"Long running command #{i++}");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Log that our operation has been canceled
                    Debug.WriteLine("Bailing out...");
                }
            }

            private void OnCancelInitCommand()
            {
                this.initializationCancelToken.Cancel();
            }

            private bool OnCanTestAsyncCommandHandler(string arg)
            {
                return this.Delay > TimeSpan.Zero && !string.IsNullOrEmpty(this.Input);
            }

            private async Task OnTestAsyncCommandHandler(string arg, CancellationToken cancellationToken)
            {
                try
                {
                    // Wait a little while (async)
                    await Task.Delay(this.Delay, cancellationToken).ConfigureAwait(true);

                    // Return the reversed string
                    this.Output = string.Join("", arg.Reverse());
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation
                    this.Output = "--canceled--";
                }
            }

            public void CancelInitialization()
            {
                this.initializationCancelToken.Cancel();
            }
        }

        private readonly DataModel dataModel = new DataModel();

        public MainWindow()
        {
            // Initialize component
            this.InitializeComponent();

            // Dispose the data model when the window is closed
            // (user controls better use the Unload event)
            this.Closed += (s, e) => this.dataModel.Dispose();

            // Load the data model
            this.DataContext = this.dataModel;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            this.dataModel.CancelInitialization();
        }
    }
}