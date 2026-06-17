namespace Stanza.TerminalGui;

/// <summary>
/// Base attribute for all Stanza MVVM bindings.
/// This attribute provides the core logic for identifying which ViewModel property
/// a UI element should synchronize with.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = true
)]
public abstract class StanzaBindingAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the ViewModel property to bind to.
    /// </summary>
    public string ViewModelPropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StanzaBindingAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the property on the ViewModel. Use <c>nameof()</c> for refactoring safety.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="viewModelPropertyName"/> is null or whitespace.</exception>
    protected StanzaBindingAttribute(string viewModelPropertyName)
    {
        if (string.IsNullOrWhiteSpace(viewModelPropertyName))
            throw new ArgumentException(
                "ViewModel property name cannot be null or empty.",
                nameof(viewModelPropertyName)
            );

        ViewModelPropertyName = viewModelPropertyName;
    }
}

/// <summary>
/// Binds the <c>Text</c> property of a Terminal.Gui View to a ViewModel property.
/// </summary>
/// <remarks>
/// By default, this uses <see cref="BindingMode.TwoWay"/> for input controls (like <c>TextField</c>)
/// and <see cref="BindingMode.OneWay"/> for static controls (like <c>Label</c>).
/// If the ViewModel property is read-only, the generator will automatically degrade this to <see cref="BindingMode.OneWay"/>.
/// </remarks>
public sealed class BindTextAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Gets or sets the synchronization mode. Defaults to <see cref="BindingMode.TwoWay"/>.
    /// </summary>
    public BindingMode Mode { get; set; } = BindingMode.TwoWay;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindTextAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the string-compatible property on the ViewModel.</param>
    public BindTextAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}

/// <summary>
/// Binds the <c>Checked</c> or <c>Value</c> state of a toggleable control (like <c>CheckBox</c>)
/// to a boolean ViewModel property.
/// </summary>
/// <remarks>
/// This binding requires the ViewModel property to be of type <see cref="bool"/>.
/// Defaults to <see cref="BindingMode.TwoWay"/>.
/// </remarks>
public sealed class BindCheckedAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Gets or sets the synchronization mode. Defaults to <see cref="BindingMode.TwoWay"/>.
    /// </summary>
    public BindingMode Mode { get; set; } = BindingMode.TwoWay;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindCheckedAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the boolean property on the ViewModel.</param>
    public BindCheckedAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}

/// <summary>
/// Binds the <see cref="Terminal.Gui.ViewBase.View.Visible"/> property of a View to a boolean ViewModel property.
/// </summary>
/// <remarks>
/// This is a <see cref="BindingMode.OneWay"/> binding. The UI state is driven by the ViewModel.
/// </remarks>
public sealed class BindVisibleAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindVisibleAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the boolean property on the ViewModel.</param>
    public BindVisibleAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}

/// <summary>
/// Binds the <see cref="Terminal.Gui.ViewBase.View.Enabled"/> property of a View to a boolean ViewModel property.
/// </summary>
/// <remarks>
/// This is a <see cref="BindingMode.OneWay"/> binding.
/// For <c>Button</c> types, consider using <see cref="BindCommandAttribute"/> instead, as it handles
/// the enabled state automatically via <c>CanExecute</c>.
/// </remarks>
public sealed class BindEnabledAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindEnabledAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the boolean property on the ViewModel.</param>
    public BindEnabledAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}

/// <summary>
/// Binds an <see cref="System.Windows.Input.ICommand"/> from the ViewModel to a <c>Button</c> or interactive View.
/// </summary>
/// <remarks>
/// This binding performs two actions:
/// <list type="bullet">
/// <item><description>Triggers <c>Execute</c> when the View's <c>Accepting</c> event fires.</description></item>
/// <item><description>Synchronizes the View's <c>Enabled</c> state with the command's <c>CanExecute</c> status.</description></item>
/// </list>
/// </remarks>
public sealed class BindCommandAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindCommandAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the <c>ICommand</c> property on the ViewModel.</param>
    public BindCommandAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}

public sealed class BindListAttribute : StanzaBindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindListAttribute"/> class.
    /// </summary>
    /// <param name="viewModelPropertyName">The name of the <c>ICommand</c> property on the ViewModel.</param>
    public BindListAttribute(string viewModelPropertyName)
        : base(viewModelPropertyName) { }
}
