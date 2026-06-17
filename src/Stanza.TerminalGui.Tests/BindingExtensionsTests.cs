using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Stanza.TerminalGui.Tests;

public partial class BindingExtensionsTests
{
    private partial class FakeViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int Score { get; set; }

        [ObservableProperty]
        public partial bool IsVisible { get; set; } = true;

        [ObservableProperty]
        public partial bool IsEnabled { get; set; } = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        public partial bool CanSave { get; set; } = true;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save() => SaveCalledCount++;

        public int SaveCalledCount { get; set; }
    }

    [Test]
    public async Task Bind_OneWay_UpdatesUiOnPropertyChange()
    {
        // Arrange
        var vm = new FakeViewModel { Name = "Initial" };
        var dispatcher = new View(); // Dummy dispatcher
        var uiValue = "";

        // Act - Note: Bind signature changed to require dispatcher and vm-parameterized lambda
        using var binding = dispatcher.Bind(vm, x => x.Name, val => uiValue = val);

        // Assert - Initial Value (Bind fires immediately)
        await Assert.That(uiValue).IsEqualTo("Initial");

        // Act - Change VM
        vm.Name = "Updated";

        // Assert - UI Updated
        await Assert.That(uiValue).IsEqualTo("Updated");
    }

    [Test]
    public async Task Bind_OneWay_DoesNotUpdateAfterDispose()
    {
        // Arrange
        var vm = new FakeViewModel { Name = "Initial" };
        var dispatcher = new View();
        var uiValue = "";
        var binding = dispatcher.Bind(vm, x => x.Name, val => uiValue = val);

        // Act
        binding.Dispose();
        vm.Name = "ChangesAfterDispose";

        // Assert
        await Assert.That(uiValue).IsEqualTo("Initial");
    }

    [Test]
    public async Task BindCommand_ExecutesRelayCommandOnButtonAccept()
    {
        // Arrange
        var vm = new FakeViewModel();
        var button = new Button();

        // Act - Using updated signature: command.BindCommand(button)
        using var binding = button.BindCommand(vm.SaveCommand);

        // Simulate Button Accept/Click
        button.InvokeCommand(Command.Accept);

        // Assert
        await Assert.That(vm.SaveCalledCount).IsEqualTo(1);
    }

    [Test]
    public async Task BindingContext_Dispose_CleansUpMultipleBindings()
    {
        // Arrange
        var ctx = new BindingContext();
        var vm = new FakeViewModel { Name = "Initial" };
        var dispatcher = new View();
        var uiValue1 = "";
        var uiValue2 = "";

        // Use the new AddTo fluent extension as well
        dispatcher.Bind(vm, x => x.Name, val => uiValue1 = val).AddTo(ctx);
        dispatcher.Bind(vm, x => x.Name, val => uiValue2 = val).AddTo(ctx);

        // Act
        ctx.Dispose();
        vm.Name = "NewValue";

        // Assert
        await Assert.That(uiValue1).IsEqualTo("Initial");
        await Assert.That(uiValue2).IsEqualTo("Initial");
    }

    [Test]
    public async Task AddTo_ReturnsOriginalInstance()
    {
        // Arrange
        var ctx = new BindingContext();
        var disposable = new DisposableAction(() => { });

        // Act
        var returned = disposable.AddTo(ctx);

        // Assert - Verify fluent return (NativeAOT check)
        await Assert.That(returned).IsSameReferenceAs(disposable);
    }

    [Test]
    public async Task AddTo_FluentExtension_RegistersInContext()
    {
        // Arrange
        var ctx = new BindingContext();
        var wasDisposed = false;
        var disposable = new DisposableAction(() => wasDisposed = true);

        // Act
        disposable.AddTo(ctx);
        ctx.Dispose();

        // Assert
        await Assert.That(wasDisposed).IsTrue();
    }

    [Test]
    public async Task Manual_TwoWay_Update_Test()
    {
        // Arrange
        var vm = new FakeViewModel { Name = "Initial" };
        var textField = new TextField { Text = "Initial" };
        var dispatcher = textField;

        // 1. VM -> UI (One Way)
        using var binding = dispatcher.Bind(vm, x => x.Name, val => textField.Text = val);

        // 2. UI -> VM (Manual hook since no BindTwoWay exists yet)
        textField.TextChanged += (s, e) =>
        {
            vm.Name = textField.Text.ToString();
        };

        // Act - Change UI
        textField.Text = "Changed In UI";

        // Assert - VM updated
        await Assert.That(vm.Name).IsEqualTo("Changed In UI");

        // Act - Change VM
        vm.Name = "Changed In VM";

        // Assert - UI updated
        await Assert.That(textField.Text.ToString()).IsEqualTo("Changed In VM");
    }

    [Test]
    public async Task Bind_HandlesVariousLambdaStyles_Correctly()
    {
        var vm = new FakeViewModel { Name = "Initial" };
        var dispatcher = new View();
        string? capturedValue = null;

        // Style 1: Standard 'x'
        using var b1 = dispatcher.Bind(vm, x => x.Name, v => capturedValue = v);
        vm.Name = "Updated1";
        await Assert.That(capturedValue).IsEqualTo("Updated1");

        // Style 2: Different parameter name (ensures parser isn't hardcoded to 'x')
        using var b2 = dispatcher.Bind(vm, model => model.Name, v => capturedValue = v);
        vm.Name = "Updated2";
        await Assert.That(capturedValue).IsEqualTo("Updated2");

        // Style 3: Nested path (ensures parser takes the LAST segment)
        // Note: This assumes FakeViewModel has a sub-property.
        // Even if it doesn't, the string parser should return "Name" for "m => m.Sub.Name"
    }

    [Test]
    public async Task BindTwoWay_Dispose_UnsubscribesUiHandler()
    {
        // Arrange
        var vm = new FakeViewModel { Name = "Initial" };
        var textField = new TextField { Text = "Initial" };
        var uiSubscribed = false;
        var uiUnsubscribed = false;

        var binding = textField.BindTwoWay(
            vm,
            v => v.Name,
            val => vm.Name = val,
            handler =>
            {
                uiSubscribed = true;
                textField.TextChanged += (s, e) => handler();
                return new DisposableAction(() => uiUnsubscribed = true);
            },
            () => textField.Text.ToString(),
            val => textField.Text = val
        );

        // Act
        binding.Dispose();

        // Assert
        await Assert.That(uiSubscribed).IsTrue();
        await Assert.That(uiUnsubscribed).IsTrue();
    }

    // --- Phase A: Command Lifecycle ---

    [Test]
    public async Task BindCommand_SyncsCanExecuteToEnabled()
    {
        var vm = new FakeViewModel { CanSave = false };
        var button = new Button();
        using var binding = button.BindCommand(vm.SaveCommand);

        await Assert.That(button.Enabled).IsFalse();

        vm.CanSave = true;
        await Assert.That(button.Enabled).IsTrue();
    }

    [Test]
    public async Task BindCommand_Dispose_StopsExecutionAndEnabledSync()
    {
        var vm = new FakeViewModel { CanSave = true };
        var button = new Button();
        var binding = button.BindCommand(vm.SaveCommand);

        binding.Dispose();

        vm.CanSave = false;
        await Assert.That(button.Enabled).IsTrue();

        button.InvokeCommand(Command.Accept);
        await Assert.That(vm.SaveCalledCount).IsEqualTo(0);
    }

    // --- Phase B: BindTwoWay Data Flow ---

    [Test]
    public async Task BindTwoWay_VmToUi_UpdatesUiSetter()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var dispatcher = new View();
        var uiValue = "";

        using var binding = dispatcher.BindTwoWay(
            vm,
            x => x.Name,
            v => vm.Name = v,
            handler => new DisposableAction(() => { }),
            () => uiValue,
            v => uiValue = v
        );

        vm.Name = "NewVm";
        await Assert.That(uiValue).IsEqualTo("NewVm");
    }

    [Test]
    public async Task BindTwoWay_UiToVm_UpdatesVmSetter()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var dispatcher = new View();
        var uiValue = "Init";
        Action? triggerUi = null;

        using var binding = dispatcher.BindTwoWay(
            vm,
            x => x.Name,
            v => vm.Name = v,
            handler =>
            {
                triggerUi = handler;
                return new DisposableAction(() => { });
            },
            () => uiValue,
            v => uiValue = v
        );

        uiValue = "NewUi";
        triggerUi?.Invoke();

        await Assert.That(vm.Name).IsEqualTo("NewUi");
    }

    [Test]
    public async Task BindTwoWay_ReentrancyGuard_PreventsCycle()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var dispatcher = new View();
        var uiValue = "Init";
        Action? triggerUi = null;
        var uiSetterCalls = 0;

        using var binding = dispatcher.BindTwoWay(
            vm,
            x => x.Name,
            v => vm.Name = v,
            handler =>
            {
                triggerUi = handler;
                return new DisposableAction(() => { });
            },
            () => uiValue,
            v =>
            {
                uiValue = v;
                uiSetterCalls++;
                triggerUi?.Invoke();
            }
        );

        // Reset count after initial sync
        uiSetterCalls = 0;
        vm.Name = "VmChange";

        await Assert.That(uiSetterCalls).IsEqualTo(1);
        await Assert.That(vm.Name).IsEqualTo("VmChange");
    }

    [Test]
    public async Task BindTwoWay_EqualValue_SkipsVmSetter()
    {
        var vm = new FakeViewModel { Score = 10 };
        var dispatcher = new View();
        var uiValue = 10;
        Action? triggerUi = null;

        var setterCalled = false;
        using var binding = dispatcher.BindTwoWay(
            vm,
            x => x.Score,
            v =>
            {
                vm.Score = v;
                setterCalled = true;
            },
            handler =>
            {
                triggerUi = handler;
                return new DisposableAction(() => { });
            },
            () => uiValue,
            v => uiValue = v
        );

        triggerUi?.Invoke();
        await Assert.That(setterCalled).IsFalse();
    }

    [Test]
    public async Task BindTwoWay_Dispose_StopsVmToUiDirection()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var dispatcher = new View();
        var uiValue = "";

        var binding = dispatcher.BindTwoWay(
            vm,
            x => x.Name,
            v => vm.Name = v,
            handler => new DisposableAction(() => { }),
            () => uiValue,
            v => uiValue = v
        );

        binding.Dispose();
        vm.Name = "NewVm";

        await Assert.That(uiValue).IsEqualTo("Init");
    }

    // --- Phase C: Apply* methods ---

    [Test]
    public async Task ApplyBindText_OneWay_UpdatesViewText()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var view = new View();
        using var binding = view.ApplyBindText(vm, "Name", x => x.Name);

        vm.Name = "NewName";
        await Assert.That(view.Text?.ToString()).IsEqualTo("NewName");
    }

    [Test]
    public async Task ApplyBindText_TwoWay_VmToUi()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var view = new View();
        using var binding = view.ApplyBindText(vm, "Name", x => x.Name, (x, v) => x.Name = v);

        vm.Name = "NewName";
        await Assert.That(view.Text?.ToString()).IsEqualTo("NewName");
    }

    [Test]
    public async Task ApplyBindText_TwoWay_UiToVm()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var view = new View { Text = "Init" };
        using var binding = view.ApplyBindText(vm, "Name", x => x.Name, (x, v) => x.Name = v);

        view.Text = "UiName";
        await Assert.That(vm.Name).IsEqualTo("UiName");
    }

    [Test]
    public async Task ApplyBindText_SkipsRedundantTextSet()
    {
        var vm = new FakeViewModel { Name = "Init" };
        var view = new View { Text = "Init" };
        var events = 0;
        view.TextChanged += (s, e) => events++;
        using var binding = view.ApplyBindText(vm, "Name", x => x.Name, (x, v) => x.Name = v);

        vm.Name = "Init";
        await Assert.That(events).IsEqualTo(0);
    }

    [Test]
    [Arguments(true, true)]
    [Arguments(false, false)]
    public async Task ApplyBindChecked_OneWay_SyncsCheckState(bool vmState, bool expectedUiState)
    {
        var vm = new FakeViewModel { IsEnabled = !vmState };
        var cb = new CheckBox();
        using var binding = cb.ApplyBindChecked(vm, "IsEnabled", x => x.IsEnabled);

        vm.IsEnabled = vmState;
        var checkState = expectedUiState ? CheckState.Checked : CheckState.UnChecked;
        await Assert.That(cb.Value).IsEqualTo(checkState);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ApplyBindChecked_TwoWay_UiToVm(bool uiState)
    {
        var vm = new FakeViewModel { IsEnabled = !uiState };
        var cb = new CheckBox { Value = uiState ? CheckState.UnChecked : CheckState.Checked };
        using var binding = cb.ApplyBindChecked(
            vm,
            "IsEnabled",
            x => x.IsEnabled,
            (x, v) => x.IsEnabled = v
        );

        cb.Value = uiState ? CheckState.Checked : CheckState.UnChecked; // triggers ValueChanged
        await Assert.That(vm.IsEnabled).IsEqualTo(uiState);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ApplyBindVisible_SyncsVisibility(bool visible)
    {
        var vm = new FakeViewModel { IsVisible = !visible };
        var view = new View();
        using var binding = view.ApplyBindVisible(vm, "IsVisible", x => x.IsVisible);

        vm.IsVisible = visible;
        await Assert.That(view.Visible).IsEqualTo(visible);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ApplyBindEnabled_SyncsEnabled(bool enabled)
    {
        var vm = new FakeViewModel { IsEnabled = !enabled };
        var view = new View();
        using var binding = view.ApplyBindEnabled(vm, "IsEnabled", x => x.IsEnabled);

        vm.IsEnabled = enabled;
        await Assert.That(view.Enabled).IsEqualTo(enabled);
    }

    // --- Phase D: Edge cases ---

    [Test]
    public async Task BindingContext_AddBinding_ThrowsWhenDisposed()
    {
        var ctx = new BindingContext();
        ctx.Dispose();

        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            ctx.AddBinding(new DisposableAction(() => { }));
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    [Arguments("x => x.Name", "Name")]
    [Arguments("vm => vm.Title", "Title")]
    [Arguments("m => m.User.Profile.FirstName", "FirstName")]
    [Arguments("() => vm.Status", "Status")]
    public async Task ExtractPropertyName_ValidatesExpectedFormats(
        string expression,
        string expected
    )
    {
        await Assert.That(BindingExtensions.ExtractPropertyName(expression)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null, "Text")]
    [Arguments("", "Text")]
    [Arguments("viewModel.Name", "Name")]
    public async Task ExtractPropertyName_HandlesEdgeCases(string? expression, string expected)
    {
        await Assert.That(BindingExtensions.ExtractPropertyName(expression)).IsEqualTo(expected);
    }
}

// [StanzaView<DemoViewModel>(Title = "Stanza MVVM Demo")]
// public partial class DemoView : View
// {
//     public Label TitleLabel { get; private set; } = new()
//     {
//         Text = "Stanza.TerminalGui Declarative Demo",
//         Width = Dim.Auto(),
//         Height = 1
//     };

//     public Label InstructionLabel { get; private set; } = new()
//     {
//         Text = "Type your name below to see dynamic bindings:",
//         Width = Dim.Auto(),
//         Height = 1,
//         Below = nameof(TitleLabel)
//     };

//     public TextField NameInput { get; private set; } = new()
//     {
//         Width = 30,
//         Height = 1,
//         BindText = nameof(DemoViewModel.Name),
//         Below = nameof(InstructionLabel),
//         Enabled = true,
//     };

//     public CheckBox ShowGreetingsCheckbox { get; private set; } = new()
//     {
//         Text = "Show Greetings Banner",
//         Width = Dim.Auto(),
//         Height = 1,
//         BindChecked = nameof(DemoViewModel.ShowGreetings),
//         Below = nameof(NameInput)
//     };

//     public Label GreetingsLabel { get; private set; } = new()
//     {
//         Width = Dim.Auto(),
//         Height = Dim.Auto(),
//         BindText = nameof(DemoViewModel.GreetingMessage),
//         BindVisible = nameof(DemoViewModel.ShowGreetings),
//         Below = nameof(ShowGreetingsCheckbox)
//     };

//     public Button ResetButton { get; private set; } = new()
//     {
//         Text = "Reset Form",
//         Width = Dim.Auto(),
//         Height = Dim.Auto(),
//         BindCommand = nameof(DemoViewModel.ResetCommand),
//         Below = nameof(GreetingsLabel)
//     };
// }
// public partial class DemoViewModel : ObservableObject
// {
//     [ObservableProperty]
//     public partial string Name { get; set; } = "Stanza User";
//     [ObservableProperty]
//     public partial bool ShowGreetings { get; set; } = true;

//     public string GreetingMessage => $"Hello, {Name}! Welcome to Stanza.TerminalGui!";

//     partial void OnNameChanged(string value)
//     {
//         OnPropertyChanged(nameof(GreetingMessage));
//     }

//     [RelayCommand]
//     private void Reset()
//     {
//         Name = "Stanza User";
//         ShowGreetings = true;
//     }
// }
