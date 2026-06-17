using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

[assembly: InternalsVisibleTo("Stanza.TerminalGui.Tests")]

namespace Stanza.TerminalGui;

/// <summary>
/// Provides high-performance, reflection-free extension methods for binding
/// ViewModel properties to Terminal.Gui Views.
/// </summary>
/// <remarks>
/// These methods are designed to be NativeAOT compatible by avoiding runtime reflection
/// and utilizing compile-time captured member names via <see cref="CallerArgumentExpressionAttribute"/>.
/// </remarks>
public static class BindingExtensions
{
    /// <summary>
    /// Establishes a thread-safe, one-way binding between a ViewModel property and a UI update action.
    /// Automatically marshals property updates to the Terminal.Gui Main Loop using the provided <paramref name="view"/>.
    /// </summary>
    /// <typeparam name="TViewModel">The type of the ViewModel, which must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value being bound.</typeparam>
    /// <param name="viewModel">The ViewModel instance containing the source property.</param>
    /// <param name="view">The <see cref="View"/> used to access the application main loop for thread-safe UI updates.</param>
    /// <param name="propertyExpression">A lambda expression used to retrieve the current property value from the ViewModel.</param>
    /// <param name="updateUi">An action executed on the UI thread whenever the property value changes.</param>
    /// <param name="expression">The string representation of the property expression, automatically captured at compile-time via <see cref="CallerArgumentExpressionAttribute"/>.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the property change subscription and stops UI synchronization when disposed.</returns>
    public static IDisposable Bind<TViewModel, TValue>(
        this View view,
        TViewModel viewModel,
        Func<TViewModel, TValue> propertyExpression,
        Action<TValue> updateUi,
        [CallerArgumentExpression(nameof(propertyExpression))] string? expression = null
    )
        where TViewModel : INotifyPropertyChanged
    {
        string propertyName = ExtractPropertyName(expression);

        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (string.Equals(e.PropertyName, propertyName, StringComparison.Ordinal))
            {
                var newValue = propertyExpression(viewModel);
                StanzaConfig.Trace($"[Binding] VM -> UI: {propertyName} = {newValue}");

                if (view.App != null)
                    view.App.Invoke(() => updateUi(newValue));
                else
                    updateUi(newValue);
            }
        };

        viewModel.PropertyChanged += handler;

        var initialValue = propertyExpression(viewModel);
        if (view.App != null)
            view.App.Invoke(() => updateUi(initialValue));
        else
            updateUi(initialValue);

        return new DisposableAction(() => viewModel.PropertyChanged -= handler);
    }

    /// <summary>
    /// Binds an <see cref="ICommand"/> to a Terminal.Gui <see cref="Button"/>.
    /// Synchronizes the button's <see cref="View.Enabled"/> state with <see cref="ICommand.CanExecute"/>
    /// and triggers <see cref="ICommand.Execute"/> when the button is accepted.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="button">The target button that will trigger the command and reflect its enabled state.</param>
    /// <returns>An <see cref="IDisposable"/> that unhooks the command and button events when disposed.</returns>
    public static IDisposable BindCommand(this Button button, ICommand command)
    {
        void UpdateEnabled(object? s, EventArgs e)
        {
            if (button.App != null)
                button.App.Invoke(() => button.Enabled = command.CanExecute(null)); // Safe marshal [1]
            else
                button.Enabled = command.CanExecute(null);
        }

        void OnAccept(object? s, EventArgs e) => command.Execute(null);

        command.CanExecuteChanged += UpdateEnabled;
        button.Accepting += OnAccept;

        UpdateEnabled(null, EventArgs.Empty);

        return new DisposableAction(() =>
        {
            command.CanExecuteChanged -= UpdateEnabled;
            button.Accepting -= OnAccept;
        });
    }

    /// <summary>
    /// Establishes a thread-safe, two-way binding between a ViewModel property and a UI element.
    /// </summary>
    public static IDisposable BindTwoWay<TViewModel, TValue>(
        this View view,
        TViewModel viewModel,
        Func<TViewModel, TValue> vmGetter,
        Action<TValue> vmSetter,
        Func<Action, IDisposable> subscribeUiChange,
        Func<TValue> uiGetter,
        Action<TValue> uiSetter,
        [CallerArgumentExpression(nameof(vmGetter))] string? expression = null
    )
        where TViewModel : INotifyPropertyChanged
    {
        string propertyName = ExtractPropertyName(expression);
        bool updating = false;

        var vmToUi = view.Bind(
            viewModel,
            vmGetter,
            val =>
            {
                if (updating)
                    return;
                updating = true;
                try
                {
                    uiSetter(val);
                }
                finally
                {
                    updating = false;
                }
            },
            expression
        );

        var uiHandler = new Action(() =>
        {
            if (updating)
                return;

            var newVal = uiGetter();
            if (EqualityComparer<TValue>.Default.Equals(newVal, vmGetter(viewModel)))
                return;

            updating = true;
            try
            {
                vmSetter(newVal);
            }
            finally
            {
                updating = false;
            }
        });

        var uiSub = subscribeUiChange(uiHandler);

        return new DisposableAction(() =>
        {
            vmToUi.Dispose();
            uiSub.Dispose();
        });
    }

    public static IDisposable OnEvent<TArgs>(
        this View view,
        Action<EventHandler<TArgs>> subscribe,
        Action<EventHandler<TArgs>> unsubscribe,
        Action<TArgs> handler
    )
    {
        EventHandler<TArgs> wrapper = (s, e) => handler(e);
        subscribe(wrapper);
        return new DisposableAction(() => unsubscribe(wrapper));
    }

    public static IDisposable OnCollectionChanged(
        this INotifyCollectionChanged collection,
        NotifyCollectionChangedEventHandler handler
    )
    {
        collection.CollectionChanged += handler;
        return new DisposableAction(() => collection.CollectionChanged -= handler);
    }

    public static IDisposable OnPropertyChanged(
        this INotifyPropertyChanged viewModel,
        string propertyName,
        Action handler
    )
    {
        PropertyChangedEventHandler wrapper = (s, e) =>
        {
            if (e.PropertyName == propertyName)
                handler();
        };
        viewModel.PropertyChanged += wrapper;
        return new DisposableAction(() => viewModel.PropertyChanged -= wrapper);
    }

    #region Generator Apply Methods

    /// <summary>
    /// Generator target for text binding.
    /// </summary>
    public static IDisposable ApplyBindText<TViewModel>(
        this View view,
        TViewModel viewModel,
        string propertyName,
        Func<TViewModel, object> getter,
        Action<TViewModel, string>? setter = null
    )
        where TViewModel : INotifyPropertyChanged
    {
        if (setter != null)
        {
            return view.BindTwoWay(
                viewModel,
                getter,
                val => setter(viewModel, val?.ToString() ?? string.Empty),
                handler =>
                {
                    EventHandler internalHandler = (s, e) => handler();
                    view.TextChanged += internalHandler;
                    return new DisposableAction(() => view.TextChanged -= internalHandler);
                },
                () => view.Text?.ToString() ?? string.Empty,
                val =>
                {
                    var newText = val?.ToString() ?? string.Empty;
                    // CRITICAL FIX: Only set if the text is actually different.
                    // This prevents the cursor-reset loop.
                    if (view.Text != newText)
                    {
                        view.Text = newText;
                    }
                },
                propertyName
            );
        }
        else
        {
            return view.Bind(
                viewModel,
                getter,
                val =>
                {
                    var newText = val?.ToString() ?? string.Empty;
                    if (view.Text != newText)
                    {
                        view.Text = newText;
                    }
                },
                propertyName
            );
        }
    }

    /// <summary>
    /// Generator target for checked binding.
    /// </summary>
    public static IDisposable ApplyBindChecked<TViewModel>(
        this CheckBox target,
        TViewModel viewModel,
        string propertyName,
        Func<TViewModel, bool> getter,
        Action<TViewModel, bool>? setter = null
    )
        where TViewModel : INotifyPropertyChanged
    {
        if (setter != null)
        {
            return target.BindTwoWay(
                viewModel,
                getter,
                val => setter(viewModel, val),
                handler =>
                {
                    EventHandler<ValueChangedEventArgs<CheckState>> internalHandler = (s, e) =>
                        handler();
                    target.ValueChanged += internalHandler;
                    return new DisposableAction(() => target.ValueChanged -= internalHandler);
                },
                () => target.Value == CheckState.Checked,
                val => target.Value = val ? CheckState.Checked : CheckState.UnChecked,
                propertyName
            );
        }
        else
        {
            return target.Bind(
                viewModel,
                getter,
                val => target.Value = val ? CheckState.Checked : CheckState.UnChecked,
                propertyName
            );
        }
    }

    /// <summary>
    /// Generator target for visible binding.
    /// </summary>
    public static IDisposable ApplyBindVisible<TViewModel>(
        this View target,
        TViewModel viewModel,
        string propertyName,
        Func<TViewModel, bool> getter
    )
        where TViewModel : INotifyPropertyChanged
    {
        return target.Bind(viewModel, getter, val => target.Visible = val, propertyName);
    }

    /// <summary>
    /// Generator target for enabled binding.
    /// </summary>
    public static IDisposable ApplyBindEnabled<TViewModel>(
        this View target,
        TViewModel viewModel,
        string propertyName,
        Func<TViewModel, bool> getter
    )
        where TViewModel : INotifyPropertyChanged
    {
        return target.Bind(viewModel, getter, val => target.Enabled = val, propertyName);
    }

    /// <summary>
    /// Generator target for command binding.
    /// </summary>
    public static IDisposable ApplyBindCommand(this Button target, ICommand command)
    {
        return target.BindCommand(command);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Robust, non-reflection compile-time expression string parsing.
    /// Handles standard member lambdas, full paths, and local scope expressions cleanly [1].
    /// </summary>
    internal static string ExtractPropertyName(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "Text";
        }

        if (expression.Contains("=>"))
        {
            expression = expression.Split(["=>"], StringSplitOptions.None).Last().Trim();
        }

        string propertyName = expression.Split('.').Last().Trim('(', ')', ' ');

        return propertyName;
    }

    #endregion
}
