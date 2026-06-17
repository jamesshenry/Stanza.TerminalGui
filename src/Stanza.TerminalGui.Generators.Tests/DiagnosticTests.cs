using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Stanza.TerminalGui.Generators.Tests;

public class DiagnosticTests
{
    [Test]
    public async Task STN005_Error_OnExplicitTwoWayReadOnly()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                public string ReadOnlyProp => "Hello";
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                [BindText(nameof(MyVm.ReadOnlyProp), Mode = BindingMode.TwoWay)]
                public Label MyLabel { get; set; } = new();
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN005");
    }

    [Test]
    public async Task STN005_Info_AutoDegradesDefaultTwoWayToOneWay()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class ReadOnlyVm : System.ComponentModel.INotifyPropertyChanged {
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                public string ReadOnlyProp => "Constant";
            }

            [StanzaView<ReadOnlyVm>]
            public partial class MyView : View {
                [BindText("ReadOnlyProp")] // Default is TwoWay, but prop is read-only
                public TextField MyInput { get; set; } = new();
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);
        var generated = TestHelper.GetGeneratedSource(source, "MyView.g.cs");

        // 1. Verify the diagnostic info message
        await Assert
            .That(diags)
            .Contains(d => d.Id == "STN005" && d.Severity == DiagnosticSeverity.Info);

        // 2. Verify generated code does NOT have a setter lambda
        await Assert.That(generated).Contains("x => x.ReadOnlyProp)");
        await Assert.That(generated).DoesNotContain("val) => x.ReadOnlyProp = val");
    }

    [Test]
    public async Task STN010_Error_WhenClassIsNotPartial()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public class MyView : View { } // Missing 'partial'
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags.Any(d => d.Id == "STN010")).IsTrue();
    }

    [Test]
    public async Task STN011_Error_WhenNotInheritingFromView()
    {
        var source = """
            using Stanza.TerminalGui;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView { } // Doesn't inherit from View
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags.Any(d => d.Id == "STN011")).IsTrue();
    }

    [Test]
    public async Task STN020_Error_WhenPropertyDoesNotExistOnVm()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                [BindText("NonExistentProperty")]
                public Label MyLabel { get; set; } = new();
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN020");
    }

    [Test]
    public async Task STN030_Error_WhenBindCheckedUsedOnString()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                public string NotABool { get; set; } = "";
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                [BindChecked(nameof(MyVm.NotABool))]
                public CheckBox MyCheck { get; set; } = new();
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN030");
    }

    [Test]
    public async Task STN012_Error_OnMemberCollision()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                // Collision with generated plumbing
                public string ViewModel { get; set; } = ""; 
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN012");
    }

    [Test]
    public async Task STN040_Error_WhenViewModelDoesNotImplementINPC()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class NotInpcVm { 
                public string Name { get; set; } = "";
            }

            [StanzaView<NotInpcVm>]
            public partial class MyView : View { }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN040");
    }

    [Test]
    public async Task STN041_Warning_OnUnmanagedEventSubscriptionInCtor()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                public Button MyButton { get; set; } = new();
                public MyView() {
                    MyButton.Accepting += (s, e) => { }; // Smell!
                }
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN041");
    }

    [Test]
    public async Task STN041_Warning_OnUnmanagedSubscriptionInApplyBindings()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                public Button MyButton { get; set; } = new();
                partial void OnApplyBindings(BindingContext context) {
                    MyButton.Accepting += (s, e) => { }; // Smell!
                }
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN041");
    }

    [Test]
    public async Task STN041_NoError_WhenSubscriptionIsManaged()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                public Button MyButton { get; set; } = new();
                partial void OnApplyBindings(BindingContext context) {
                    // This is fine
                    new System.Action(() => { }).AddTo(context);
                }
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags.Any(d => d.Id == "STN041")).IsFalse();
    }

    [Test]
    public async Task STN031_Error_WhenBindCheckedOnNonCheckBox()
    {
        var source = """
            using Stanza.TerminalGui;
            using Terminal.Gui.Views;

            public class MyVm : System.ComponentModel.INotifyPropertyChanged { 
                public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                public bool IsActive { get; set; }
            }

            [StanzaView<MyVm>]
            public partial class MyView : View {
                [BindChecked(nameof(MyVm.IsActive))]
                public Button MyButton { get; set; } = new(); // Invalid target for BindChecked
            }
            """;

        var diags = TestHelper.VerifyDiagnostics(source);

        await Assert.That(diags).Contains(d => d.Id == "STN031");
    }
}
