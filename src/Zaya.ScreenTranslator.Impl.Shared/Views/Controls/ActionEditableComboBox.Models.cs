using System.ComponentModel;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

public sealed class ActionEditableComboBoxAction
{
    public ActionEditableComboBoxAction(string id, string header)
    {
        Id = id;
        Header = header;
    }

    public string Id { get; }
    public string Header { get; }
}

public sealed class ActionEditableComboBoxActionEventArgs : EventArgs
{
    public ActionEditableComboBoxActionEventArgs(string actionId) => ActionId = actionId;
    public string ActionId { get; }
}

public sealed class ActionEditableComboBoxItemEventArgs : EventArgs
{
    public ActionEditableComboBoxItemEventArgs(object item) => Item = item;
    public object Item { get; }
}

public sealed class ActionEditableComboBoxRenameEventArgs : CancelEventArgs
{
    public ActionEditableComboBoxRenameEventArgs(string newName) => NewName = newName;
    public string NewName { get; }
}

/// <summary>Internal dropdown row: action, separator, or selectable item.</summary>
internal sealed class ActionEditableComboBoxEntry
{
    public static ActionEditableComboBoxEntry Separator { get; } = new() { IsSeparator = true };

    public string Display { get; init; } = string.Empty;
    public string? ActionId { get; init; }
    public object? Item { get; init; }
    public bool IsSeparator { get; init; }
    public bool IsAction => ActionId is not null;

    public override string ToString() => Display;
}
