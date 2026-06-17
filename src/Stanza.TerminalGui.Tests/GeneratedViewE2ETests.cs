using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Stanza.TerminalGui.Tests;

public partial class SimpleViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = "John";
}

[StanzaView<SimpleViewModel>]
public partial class SimpleFormView : View
{
    public Label TitleLabel { get; private set; } = new() { Text = "Form" };

    [BindText(nameof(SimpleViewModel.Name))]
    public TextField NameInput { get; private set; } = new();

    public Button SubmitButton { get; private set; } = new() { Text = "Submit" };

    public SimpleFormView()
    {
        NameInput.Y = Pos.Bottom(TitleLabel);
        SubmitButton.Y = Pos.Bottom(NameInput);
        Add(TitleLabel, NameInput, SubmitButton);
    }
}

public class GeneratedViewE2ETests
{
    [Test]
    public async Task View_Initializes_Successfully()
    {
        var vm = new SimpleViewModel { Name = "Alice" };
        var view = new SimpleFormView { ViewModel = vm };

        await Assert.That(view).IsNotNull();
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("Alice");
    }

    [Test]
    public async Task View_TwoWayBinding_UpdatesViewModelFromUi()
    {
        var vm = new SimpleViewModel { Name = "Alice" };
        var view = new SimpleFormView { ViewModel = vm };

        // Act - Simulate typing in UI
        view.NameInput.Text = "Bob";

        // Assert - ViewModel property updated
        await Assert.That(vm.Name).IsEqualTo("Bob");
    }

    [Test]
    public async Task View_TwoWayBinding_UpdatesUiFromViewModel()
    {
        var vm = new SimpleViewModel { Name = "Alice" };
        var view = new SimpleFormView { ViewModel = vm };

        // Act - Update ViewModel property
        vm.Name = "Charlie";

        // Assert - UI elements updated
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("Charlie");
    }
}
