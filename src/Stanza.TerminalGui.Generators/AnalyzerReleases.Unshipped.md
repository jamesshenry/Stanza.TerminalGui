### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
STN010 | Usage    | Error    | Class must be partial.
STN011 | Usage    | Error    | Must inherit from Terminal.Gui.View.
STN012 | Usage    | Error    | Member name collision with generated plumbing.
STN020 | Stanza.Binding | Warning    | Property name not found on ViewModel.
STN021 | Stanza.Binding | Error    | Property found but not accessible.
STN022 | Stanza.Binding | Error    | BindCommand target is not ICommand.
STN004 | Stanza.Binding | Error    | Read-only property cannot have explicit TwoWay binding.
STN005 | Stanza.Binding | Info     | Read-only property auto-degraded to OneWay.
STN030 | Stanza.Binding | Error    | ViewModel type is incompatible with Binding type (e.g. string vs bool).
STN031 | Stanza.Binding | Error    | Control type does not support this specific binding.
STN032 | Stanza.Binding | Warning  | Multiple bindings targeting the same UI property.
STN040 | Stanza.Binding | Error | StanzaDiagnostics
STN041 | Stanza.Binding | Warning | StanzaDiagnostics
