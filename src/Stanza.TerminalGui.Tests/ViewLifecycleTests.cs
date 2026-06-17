using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Stanza.TerminalGui;
using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Stanza.TerminalGui.Tests;

public partial class LifecycleTestViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;
}

[StanzaView<LifecycleTestViewModel>]
public partial class LifecycleTestView : View
{
    public Label HeaderLabel { get; private set; } = new() { Text = "Header" };

    [BindText(nameof(LifecycleTestViewModel.Name))]
    public TextField NameInput { get; private set; } = new();

    public LifecycleTestView()
    {
        NameInput.Y = Pos.Bottom(HeaderLabel);
        Add(HeaderLabel, NameInput);
    }
}

public class ViewLifecycleTests
{
    [Test]
    public async Task ViewModel_NullDetachment_UnsubscribesOldVm()
    {
        var vmA = new LifecycleTestViewModel { Name = "Alpha" };
        var view = new LifecycleTestView { ViewModel = vmA };

        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("Alpha");

        view.ViewModel = null;
        vmA.Name = "ShouldNotFlow";

        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("Alpha");
    }

    [Test]
    public async Task ViewModel_SwapNullSwap_RecoversWithoutStaleSubscriptions()
    {
        var vmA = new LifecycleTestViewModel { Name = "VM_A" };
        var vmB = new LifecycleTestViewModel { Name = "VM_B" };
        var view = new LifecycleTestView();

        view.ViewModel = vmA;
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("VM_A");

        view.ViewModel = null;
        view.ViewModel = vmB;
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("VM_B");

        vmA.Name = "VM_A_Changed";
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("VM_B");

        vmB.Name = "VM_B_Changed";
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("VM_B_Changed");
    }

    [Test]
    public async Task ViewModel_Swap_DetachesOldTwoWayUiHandler()
    {
        // This test ensures that when the ViewModel is swapped,
        // the old two-way UI handlers are detached and don't affect the old VM.
        var vmA = new LifecycleTestViewModel { Name = "VM_A" };
        var vmB = new LifecycleTestViewModel { Name = "VM_B" };
        var view = new LifecycleTestView { ViewModel = vmA };

        // Act - Swap VM
        view.ViewModel = vmB;

        // Act - Change UI (should update vmB, but NOT vmA)
        view.NameInput.Text = "UI_Changed";

        // Assert
        await Assert.That(vmB.Name).IsEqualTo("UI_Changed");
        await Assert.That(vmA.Name).IsEqualTo("VM_A");
    }

    [Test]
    public async Task ViewModel_SameInstance_IsNoOp()
    {
        var vmA = new LifecycleTestViewModel { Name = "Alpha" };
        var view = new LifecycleTestView { ViewModel = vmA };

        // Change UI text
        view.NameInput.Text = "Beta";

        // Re-assign same ViewModel instance
        view.ViewModel = vmA;

        // Ensure the value wasn't wiped back to 'Alpha' by a re-trigger of ApplyBindings
        await Assert.That(view.NameInput.Text.ToString()).IsEqualTo("Beta");
    }
}
