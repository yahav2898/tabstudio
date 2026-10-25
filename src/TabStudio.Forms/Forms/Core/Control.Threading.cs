using System.Reflection;
using System.Runtime.ExceptionServices;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>Running code on the control's UI thread from other threads.</summary>
public partial class Control
{
    /// <summary>Runs a delegate on the control's UI thread later, without waiting.</summary>
    /// <param name="method">The delegate.</param>
    public IAsyncResult BeginInvoke(Delegate method) => BeginInvoke(method, null);

    /// <summary>Runs an action on the control's UI thread later, without waiting.</summary>
    /// <param name="method">The action.</param>
    public IAsyncResult BeginInvoke(Action method) => BeginInvoke(method, null);

    /// <summary>Runs a delegate with arguments on the control's UI thread later, without waiting.</summary>
    /// <param name="method">The delegate.</param>
    /// <param name="args">The arguments.</param>
    public IAsyncResult BeginInvoke(Delegate method, params object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        var runtime = RequireRuntime();
        var result = new InvokeResult();
        runtime.Post(() => result.Run(method, args, rethrow: true));
        return result;
    }

    /// <summary>Waits for a <see cref="BeginInvoke(Delegate)"/> to finish and returns its result.</summary>
    /// <param name="asyncResult">What BeginInvoke returned.</param>
    public object? EndInvoke(IAsyncResult asyncResult)
    {
        ArgumentNullException.ThrowIfNull(asyncResult);
        if (asyncResult is not InvokeResult result)
        {
            throw new ArgumentException("The IAsyncResult was not returned by BeginInvoke of this control.", nameof(asyncResult));
        }

        if (!result.IsCompleted && Window?.Runtime is { IsOnThread: true } runtime)
        {
            runtime.RunLoop(() => result.IsCompleted);
        }

        result.AsyncWaitHandle.WaitOne();
        return result.GetResult();
    }

    /// <summary>Runs a delegate on the control's UI thread and waits for it.</summary>
    /// <param name="method">The delegate.</param>
    public object? Invoke(Delegate method) => Invoke(method, null);

    /// <summary>Runs an action on the control's UI thread and waits for it.</summary>
    /// <param name="method">The action.</param>
    public void Invoke(Action method) => Invoke((Delegate)method, null);

    /// <summary>Runs a function on the control's UI thread and waits for its result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="method">The function.</param>
    public T Invoke<T>(Func<T> method) => (T)Invoke((Delegate)method, null)!;

    /// <summary>Runs a delegate with arguments on the control's UI thread and waits for it.</summary>
    /// <param name="method">The delegate.</param>
    /// <param name="args">The arguments.</param>
    public object? Invoke(Delegate method, params object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        var runtime = RequireRuntime();
        var result = new InvokeResult();
        if (runtime.IsOnThread)
        {
            result.Run(method, args, rethrow: false);
        }
        else
        {
            runtime.Post(() => result.Run(method, args, rethrow: false));
            result.AsyncWaitHandle.WaitOne();
        }

        return result.GetResult();
    }

    private FormsRuntime RequireRuntime() =>
        Window?.Runtime ?? FormsRuntime.Current
        ?? throw new InvalidOperationException("Invoke or BeginInvoke cannot be called on a control until the window handle has been created.");

    /// <summary>The result of a delegate run on the UI thread.</summary>
    private sealed class InvokeResult : IAsyncResult
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private object? _value;
        private ExceptionDispatchInfo? _error;

        public object? AsyncState => null;

        public WaitHandle AsyncWaitHandle => ((IAsyncResult)_done.Task).AsyncWaitHandle;

        public bool CompletedSynchronously => false;

        public bool IsCompleted { get; private set; }

        /// <summary>Runs the delegate, recording its result or exception.</summary>
        /// <param name="method">The delegate.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="rethrow">Whether an exception also goes to the UI thread's handling, as for BeginInvoke.</param>
        public void Run(Delegate method, object?[]? args, bool rethrow)
        {
            try
            {
                _value = method switch
                {
                    Action action when args is null or { Length: 0 } => Run(action),
                    MethodInvoker invoker when args is null or { Length: 0 } => Run(invoker),
                    EventHandler handler when args is null or { Length: 0 } => Run(() => handler(null, EventArgs.Empty)),
                    _ => method.DynamicInvoke(args),
                };
            }
            catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
            {
                _error = ExceptionDispatchInfo.Capture(inner);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _error = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                IsCompleted = true;
                _done.TrySetResult();
            }

            if (rethrow)
            {
                _error?.Throw();
            }
        }

        public object? GetResult()
        {
            _error?.Throw();
            return _value;
        }

        private static object? Run(Action action)
        {
            action();
            return null;
        }

        private static object? Run(MethodInvoker invoker)
        {
            invoker();
            return null;
        }
    }
}
