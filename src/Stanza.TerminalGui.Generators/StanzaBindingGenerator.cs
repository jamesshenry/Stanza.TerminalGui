using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Stanza.TerminalGui.Generators;

[Generator(LanguageNames.CSharp)]
public class StanzaBindingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var viewProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Stanza.TerminalGui.StanzaViewAttribute`1",
            predicate: (node, _) => node is ClassDeclarationSyntax,
            transform: GetViewIR
        );

        context.RegisterSourceOutput(
            viewProvider,
            (spc, result) =>
            {
                foreach (var diag in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diag);
                }

                if (result.ViewIr != null)
                {
                    var source = Emitter.Emit(result.ViewIr);
                    spc.AddSource($"{result.ViewIr.ClassName}.g.cs", source);
                }
            }
        );

        var loggingProvider = context.CompilationProvider.Select(
            (compilation, _) =>
            {
                // Don't emit into the core library itself — only into consuming projects.
                if (compilation.AssemblyName == "Stanza.TerminalGui")
                    return (hasLogging: false, hasHost: false);

                var hasLogging =
                    compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILoggerFactory")
                    != null;
                var hasHost =
                    compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHost") != null;
                return (hasLogging, hasHost);
            }
        );

        context.RegisterSourceOutput(
            loggingProvider,
            (spc, flags) =>
            {
                if (!flags.hasLogging)
                    return;
                spc.AddSource(
                    "StanzaLoggingExtensions.g.cs",
                    LoggingExtensionsEmitter.Emit(flags.hasHost)
                );
            }
        );
    }

    private static ViewParseResult GetViewIR(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token
    )
    {
        var classSymbol = context.TargetSymbol as INamedTypeSymbol;
        var classDeclaration = context.TargetNode as ClassDeclarationSyntax;

        if (classSymbol == null || classDeclaration == null)
            return new ViewParseResult(null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // 1. STN010: Structural Check - Partial
        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    StanzaDiagnostics.ClassMustBePartial,
                    classDeclaration.Identifier.GetLocation(),
                    classSymbol.Name
                )
            );
            // Fatal structural error - stop here
            return new ViewParseResult(null, diagnostics.ToImmutable());
        }

        // 2. STN011: Structural Check - Inherits from View
        // We check both the specific namespace and the short name to be resilient in test environments
        if (
            !classSymbol.IsSubtypeOf("Terminal.Gui.ViewBase.View")
            && classSymbol.BaseType?.Name != "View"
        )
        {
            diagnostics.Add(
                Diagnostic.Create(
                    StanzaDiagnostics.MustInheritFromView,
                    classDeclaration.Identifier.GetLocation(),
                    classSymbol.Name
                )
            );
            return new ViewParseResult(null, diagnostics.ToImmutable());
        }

        // 3. STN012: Member Collisions
        var reservedNames = new[]
        {
            "ViewModel",
            "BindingContext",
            "_bindingContext",
            "ApplyBindings",
        };
        foreach (var reserved in reservedNames)
        {
            var existing = classSymbol.GetMembers(reserved).FirstOrDefault();
            // Only flag if it was declared in the same syntax tree (the user's source)
            if (
                existing != null
                && existing.DeclaringSyntaxReferences.Any(r =>
                    r.SyntaxTree == classDeclaration.SyntaxTree
                )
            )
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        StanzaDiagnostics.MemberCollision,
                        existing.Locations.FirstOrDefault()
                            ?? classDeclaration.Identifier.GetLocation(),
                        classSymbol.Name,
                        reserved
                    )
                );
            }
        }

        var attribute = context.Attributes.First(a =>
            a.AttributeClass?.Name.Contains("StanzaViewAttribute") == true
        );
        var vmTypeSymbol =
            attribute.AttributeClass?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;

        if (vmTypeSymbol == null)
            return new ViewParseResult(null, diagnostics.ToImmutable());

        // 4. STN040: ViewModel must implement INotifyPropertyChanged
        bool implementsInpc = vmTypeSymbol.AllInterfaces.Any(i =>
            i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged"
        );
        if (!implementsInpc)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    StanzaDiagnostics.ViewModelMustImplementINPC,
                    attribute.ApplicationSyntaxReference?.GetSyntax(token).GetLocation()
                        ?? classDeclaration.Identifier.GetLocation(),
                    vmTypeSymbol.Name
                )
            );
        }

        // 5. STN041: Unmanaged Event Subscriptions
        var methodSmells = classDeclaration
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m =>
                m.Identifier.Text == "OnApplyBindings"
                || m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword))
            );
        var ctorSmells = classDeclaration.DescendantNodes().OfType<ConstructorDeclarationSyntax>();

        foreach (var method in methodSmells.Cast<MemberDeclarationSyntax>().Concat(ctorSmells))
        {
            var addAssignments = method
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => a.IsKind(SyntaxKind.AddAssignmentExpression));

            foreach (var assignment in addAssignments)
            {
                // We're looking for += that isn't wrapped in AddBinding or followed by .AddTo
                // This is a naive heuristic but good for a "smell" diagnostic
                var parent = assignment.Parent;
                bool isManaged = false;

                // Check up the tree for AddBinding or AddTo
                while (parent != null && parent != method)
                {
                    var parentText = parent.ToString();
                    if (parentText.Contains("AddBinding") || parentText.Contains("AddTo"))
                    {
                        isManaged = true;
                        break;
                    }
                    parent = parent.Parent;
                }

                if (!isManaged)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            StanzaDiagnostics.UnmanagedEventSubscription,
                            assignment.GetLocation(),
                            method is MethodDeclarationSyntax mds
                                ? mds.Identifier.Text
                                : "constructor"
                        )
                    );
                }
            }
        }

        var vmType = vmTypeSymbol.ToDisplayString();
        var bindings = ImmutableArray.CreateBuilder<BindingIR>();
        var members = classSymbol
            .GetMembers()
            .Where(m => m is IPropertySymbol || m is IFieldSymbol);

        foreach (var member in members)
        {
            token.ThrowIfCancellationRequested();

            var memberBindings = member
                .GetAttributes()
                .Where(a =>
                    a.AttributeClass?.Name.StartsWith("Bind") == true
                    && a.AttributeClass.Name.EndsWith("Attribute")
                )
                .ToList();

            if (memberBindings.Count > 1)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        StanzaDiagnostics.DuplicateBinding,
                        member.Locations.FirstOrDefault(),
                        member.Name
                    )
                );
            }

            foreach (var attr in memberBindings)
            {
                var attrName = attr.AttributeClass!.Name;
                var bindingType = attrName.Substring(0, attrName.Length - "Attribute".Length);
                var vmPropName = attr.ConstructorArguments.FirstOrDefault().Value?.ToString();
                var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax(token).GetLocation();

                if (string.IsNullOrEmpty(vmPropName))
                    continue;

                // STN020: Resolution - Search the full hierarchy for the property
                var vmProperty = vmTypeSymbol.FindPropertyInHierarchy(vmPropName);
                if (vmProperty == null)
                {
                    // Heuristic: Is this a CommunityToolkit Command that hasn't been generated yet?
                    bool isLikelyGeneratedCommand =
                        bindingType == "BindCommand"
                        && (vmPropName?.EndsWith("Command") == true)
                        && vmTypeSymbol
                            .GetMembers(
                                vmPropName.Substring(0, vmPropName.Length - "Command".Length)
                            )
                            .Any(m => m is IMethodSymbol);

                    // If it's not a known generated pattern, we should warn the user
                    if (!isLikelyGeneratedCommand)
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                StanzaDiagnostics.PropertyNotFound,
                                attrLocation,
                                vmPropName,
                                vmType
                            )
                        );
                    }

                    // Even if it's likely generated, we must proceed with caution.
                    // We'll add it to the IR because the emitted code will use the string name anyway.
                    // If the property TRULY doesn't exist, the final C# compilation of the .g.cs will fail.
                    bindings.Add(new BindingIR(member.Name, bindingType, vmPropName!, "OneWay"));
                    continue;
                }

                // STN031: Invalid control target
                ITypeSymbol memberType = member is IPropertySymbol ps
                    ? ps.Type
                    : ((IFieldSymbol)member).Type;
                if (
                    bindingType == "BindChecked"
                    && !memberType.IsSubtypeOf("Terminal.Gui.Views.CheckBox")
                    && memberType.Name != "CheckBox"
                )
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            StanzaDiagnostics.InvalidControlTarget,
                            attrLocation,
                            bindingType,
                            member.Name
                        )
                    );
                    continue;
                }
                if (
                    bindingType == "BindCommand"
                    && !memberType.IsSubtypeOf("Terminal.Gui.Views.Button")
                    && memberType.Name != "Button"
                )
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            StanzaDiagnostics.InvalidControlTarget,
                            attrLocation,
                            bindingType,
                            member.Name
                        )
                    );
                    continue;
                }

                // STN021: Resolution - Accessibility
                if (vmProperty.DeclaredAccessibility < Accessibility.Internal)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            StanzaDiagnostics.PropertyInaccessible,
                            attrLocation,
                            vmPropName,
                            vmType
                        )
                    );
                    continue;
                }

                // STN022: Command Type Safety
                if (bindingType == "BindCommand")
                {
                    bool implementsCommand = vmProperty.Type.AllInterfaces.Any(i =>
                        i.ToDisplayString() == "System.Windows.Input.ICommand"
                        || i.Name == "ICommand"
                    );
                    if (!implementsCommand)
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                StanzaDiagnostics.NotAnICommand,
                                attrLocation,
                                vmPropName
                            )
                        );
                        continue;
                    }
                }

                // STN030: Value Type Safety
                var boolRequired = new[] { "BindChecked", "BindVisible", "BindEnabled" };
                if (
                    boolRequired.Contains(bindingType)
                    && vmProperty.Type.SpecialType != SpecialType.System_Boolean
                )
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            StanzaDiagnostics.TypeMismatch,
                            attrLocation,
                            bindingType,
                            "bool",
                            vmPropName,
                            vmProperty.Type.ToDisplayString()
                        )
                    );
                    continue;
                }

                // Read-Only Logic
                bool isReadOnly = vmProperty.IsReadOnly;
                string mode =
                    (bindingType == "BindText" || bindingType == "BindChecked")
                        ? "TwoWay"
                        : "OneWay";
                bool isExplicitMode = false;

                var modeArg = attr.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Mode");
                if (modeArg.Key != null && modeArg.Value.Value is int modeValue)
                {
                    mode = modeValue == 0 ? "OneWay" : "TwoWay";
                    isExplicitMode = true;
                }

                if (mode == "TwoWay" && isReadOnly)
                {
                    if (isExplicitMode)
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                StanzaDiagnostics.InvalidTwoWayBinding,
                                attrLocation,
                                vmPropName
                            )
                        );
                        continue;
                    }
                    else
                    {
                        mode = "OneWay";
                        diagnostics.Add(
                            Diagnostic.Create(
                                StanzaDiagnostics.AutoDegradedBinding,
                                attrLocation,
                                vmPropName
                            )
                        );
                    }
                }

                bindings.Add(new BindingIR(member.Name, bindingType, vmPropName!, mode));
            }
        }

        // Resolve Namespace safely for Emitter
        string ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? "Global"
            : classSymbol.ContainingNamespace.ToDisplayString();

        return new ViewParseResult(
            new ViewIR(ns, classSymbol.Name, vmType, bindings.ToImmutable()),
            diagnostics.ToImmutable()
        );
    }
}
