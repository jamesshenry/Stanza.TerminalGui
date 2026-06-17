using Microsoft.CodeAnalysis;

namespace Stanza.TerminalGui.Generators;

public static class StanzaDiagnostics
{
    private const string Category = "Stanza.Binding";

    // --- Structural Rules (STN01x) ---

    public static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "STN010",
        title: "Class must be partial",
        messageFormat: "The class '{0}' must be declared with the 'partial' keyword to support Stanza code generation",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MustInheritFromView = new(
        id: "STN011",
        title: "Invalid base type",
        messageFormat: "The class '{0}' must inherit from 'Terminal.Gui.View' to use StanzaView attributes",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MemberCollision = new(
        id: "STN012",
        title: "Member collision",
        messageFormat: "The class '{0}' already defines a member named '{1}'. Stanza generator needs this name for plumbing.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // --- Resolution Rules (STN02x) ---

    public static readonly DiagnosticDescriptor PropertyNotFound = new(
        id: "STN020",
        title: "ViewModel property not found",
        messageFormat: "The property '{0}' was not found on ViewModel type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PropertyInaccessible = new(
        id: "STN021",
        title: "ViewModel property inaccessible",
        messageFormat: "The property '{0}' on '{1}' must be accessible (public or internal) to be used in a binding",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotAnICommand = new(
        id: "STN022",
        title: "Invalid command type",
        messageFormat: "The property '{0}' does not implement System.Windows.Input.ICommand",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // --- Type Safety & Logic (STN03x) ---

    public static readonly DiagnosticDescriptor InvalidTwoWayBinding = new(
        id: "STN004", // Retaining your original ID
        title: "Invalid Two-Way Binding",
        messageFormat: "ViewModel property '{0}' is read-only. Explicit TwoWay binding is not allowed.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor AutoDegradedBinding = new(
        id: "STN005", // Retaining your original ID
        title: "Auto-degraded Binding",
        messageFormat: "ViewModel property '{0}' is read-only. Auto-degrading to OneWay binding.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TypeMismatch = new(
        id: "STN030",
        title: "Binding type mismatch",
        messageFormat: "Binding '{0}' expects type '{1}', but property '{2}' is of type '{3}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidControlTarget = new(
        id: "STN031",
        title: "Invalid control target",
        messageFormat: "The attribute '{0}' cannot be applied to '{1}' because it does not support the required events/properties",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateBinding = new(
        id: "STN032",
        title: "Duplicate binding",
        messageFormat: "The property '{0}' already has a binding assigned. Multiple bindings to the same UI property are not supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    // --- Smells & Safety (STN04x) ---

    public static readonly DiagnosticDescriptor ViewModelMustImplementINPC = new(
        id: "STN040",
        title: "ViewModel must implement INotifyPropertyChanged",
        messageFormat: "The ViewModel type '{0}' must implement INotifyPropertyChanged for bindings to work correctly",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnmanagedEventSubscription = new(
        id: "STN041",
        title: "Unmanaged event subscription",
        messageFormat: "Manual event subscription detected in '{0}'. This will leak when the ViewModel is swapped or the View is disposed. Route subscriptions through 'BindingContext' or use '.AddTo(context)'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
