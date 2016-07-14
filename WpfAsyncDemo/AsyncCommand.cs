using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfAsyncDemo
{
    public abstract class AsyncCommandBase : ICommand
    {
        // The following two methods must be implemented by derived classes
        public abstract bool CanExecute(object parameter = null);
        public abstract Task ExecuteAsync(object parameter = null);

        // Forward the ICommand interface methods to the abstract methods
        async void ICommand.Execute(object parameter) => await this.ExecuteAsync(parameter);

        event EventHandler ICommand.CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // Helper method that invalidates the commands
        protected void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public class AsyncCommand<TParam> : AsyncCommandBase, INotifyPropertyChanged
    {
        private sealed class CancelAsyncCommand : ICommand
        {
            private CancellationTokenSource cts;
            private bool isEnabled;

            public bool CanExecute(object parameter)
            {
                // The cancel command can be executed when the command is running
                // and not already canceled
                return this.IsEnabled;
            }

            public void Execute(object parameter)
            {
                if (this.IsEnabled)
                {
                    // Cancel the command
                    this.cts.Cancel();
                    this.cts = null;

                    // Disable the cancel command
                    this.IsEnabled = false;
                }
            }

            public event EventHandler CanExecuteChanged;

            public CancellationToken Token
            {
                get
                {
                    if (this.cts == null)
                        this.cts = new CancellationTokenSource();
                    return this.cts.Token;
                }
            }

            public bool IsEnabled
            {
                get { return this.isEnabled; }
                set
                {
                    if (value)
                    {
                        // Check if the command isn't started multiple times
                        Debug.Assert(!this.isEnabled, "Cannot enable command twice.");

                        // Enable the cancellation
                        this.isEnabled = true;
                    }
                    else
                    {
                        // Reset the cancellation token
                        this.cts = null;

                        // The command can be canceled twice
                        //  1. When the command completes
                        //  2. When the command is actively canceled
                        if (!this.isEnabled)
                            return;

                        // Disable the enabled flag
                        this.isEnabled = false;
                    }

                    // Invoke the CanExecuteChanged method
                    this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private readonly Func<TParam, CancellationToken, Task> asyncCommandFunc;
        private readonly Func<TParam, bool> canExecuteFunc;
        private readonly CancelAsyncCommand cancelCommand = new CancelAsyncCommand();
        private Task task;
        private bool isExecuting;

        public AsyncCommand(Func<TParam, CancellationToken, Task> asyncCommandFunc, Func<TParam, bool> canExecuteFunc = null)
        {
            this.asyncCommandFunc = asyncCommandFunc;
            this.canExecuteFunc = canExecuteFunc;
        }

        public ICommand CancelCommand => this.cancelCommand;

        public Task Task
        {
            get { return this.task; }
            set
            {
                // Assign the new task
                this.task = value;
                this.OnPropertyChanged();

                // We're executing if the Task is set
                this.IsExecuting = this.task != null;
            }
        }

        public bool IsExecuting
        {
            get { return this.isExecuting; }
            set
            {
                // Check if the flag has changed
                if (this.isExecuting != value)
                {
                    // Update the property
                    this.isExecuting = value;
                    this.OnPropertyChanged();

                    // Enable/disable the Cancel command
                    this.cancelCommand.IsEnabled = value;

                    // Invalidate the command's can-execute status, because it will be running at this point
                    this.RaiseCanExecuteChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            // Only cancel when allowed
            if (this.cancelCommand.CanExecute(null))
                this.cancelCommand.Execute(null);
        }

        public override bool CanExecute(object parameter = null)
        {
            // The default is that the command can execute, if it's not already executing
            if (parameter != null && !(parameter is TParam))
                return false;
            return !this.IsExecuting && ((this.canExecuteFunc == null) || this.canExecuteFunc((TParam)parameter));
        }

        public override async Task ExecuteAsync(object parameter = null)
        {
            // Start executing the async command
            this.Task = this.asyncCommandFunc((TParam)parameter, this.cancelCommand.Token);

            // Wrap the execution into a try/finally block to ensure that the task will be reset
            try
            {
                // Wait until the task runs to completion
                await this.Task;
            }
            catch (TaskCanceledException)
            {
                // NOP
            }
            catch (OperationCanceledException)
            {
                // NOP
            }
            finally
            {
                // The task has completed
                this.Task = null;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AsyncCommand : AsyncCommand<object>
    {
        public AsyncCommand(Func<CancellationToken, Task> asyncCommandFunc, Func<bool> canExecuteFunc = null)
            : base((_, ct) => asyncCommandFunc(ct), _ => canExecuteFunc?.Invoke() ?? false)
        {
        }
    }
}