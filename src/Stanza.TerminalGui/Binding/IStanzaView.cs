using System.ComponentModel;

namespace Stanza.TerminalGui;

/// <summary>
/// Defines the core contract for a Stanza-managed View.
/// This interface is typically implemented automatically by the Source Generator
/// for classes decorated with <see cref="StanzaViewAttribute{TViewModel}"/>.
/// </summary>
/// <typeparam name="TViewModel">The type of the ViewModel, which must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
public interface IStanzaView<TViewModel> : IStanzaView
    where TViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the data context for the view.
    /// Setting this property triggers binding re-synchronization and lifecycle management.
    /// </summary>
    new TViewModel? ViewModel { get; set; }
}

public interface IStanzaView : IDisposable
{
    object? ViewModel { get; set; }

    /// <summary>
    /// Gets the context managing the active bindings for the current <see cref="ViewModel"/>.
    /// </summary>
    BindingContext BindingContext { get; }
}
