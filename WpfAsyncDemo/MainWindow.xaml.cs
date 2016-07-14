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
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private bool isDisposed;

            public ICommand ReverseAsyncCommand { get; }

            public string Input { get; set; }
            public TimeSpan Delay { get; set; }
            public string Output { get; private set; }
            public INotifyTaskCompletion Initialization { get; set; }

            public DataModel()
            {
                this.ReverseAsyncCommand = new AsyncCommand<string>(this.OnTestAsyncCommandHandler, this.OnCanTestAsyncCommandHandler);
                this.Input = "Test string";
                this.Delay = TimeSpan.FromSeconds(5);

                this.Initialization = NotifyTaskCompletion.Create(this.StartLongRunningAsyncCommand(this.cts.Token));
            }

            private async Task StartLongRunningAsyncCommand(CancellationToken cancellationToken)
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

                // Cancel our long running command
                this.cts.Cancel();
            }

            public void Dispose()
            {
                if (!this.isDisposed)
                {
                    // Cancel operation
                    this.cts.Cancel();

                    // We're disposed
                    this.isDisposed = true;
                }
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
    }
}
