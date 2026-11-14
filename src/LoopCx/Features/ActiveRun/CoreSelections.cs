using System.Text.Json.Serialization;

namespace LoopCx.Features.ActiveRun;

public sealed record CoreShortcut(Guid Id, string Name, CoreStartRequest Request);
public sealed record CorePreferencesState(int Version, C1WarmupCooldownSettings WarmupCooldown, string SelectedProtocol,
    IReadOnlyList<CoreStartRequest> C1Selections, CoreStartRequest C2Selection, IReadOnlyList<CoreShortcut> Shortcuts)
{
    public static CorePreferencesState Default => new(1, new(), "C1.2",
        CoreProtocols.All.Select(static protocol => new CoreStartRequest(protocol.Id, protocol.DefaultCount)).ToArray(),
        new("C2", ContinuousMinutes: 45), []);

    [JsonIgnore]
    public CoreStartRequest SelectedC1 => C1Selections.Single(request => request.ProtocolId == SelectedProtocol);

    public void Validate()
    {
        WarmupCooldown.Validate();

        if (Version != 1 || C1Selections.Count != 3 || C1Selections.Select(static request => request.ProtocolId).Distinct().Count() != 3 ||
            C1Selections.Any(static request => request.ProtocolId == "C2") || C2Selection.ProtocolId != "C2" ||
            !C1Selections.Any(request => request.ProtocolId == SelectedProtocol) ||
            Shortcuts.Any(static shortcut => shortcut.Id == Guid.Empty || string.IsNullOrWhiteSpace(shortcut.Name) || shortcut.Request.ShortcutId is not null || shortcut.Request.ShortcutName is not null) ||
            Shortcuts.Select(static shortcut => shortcut.Id).Distinct().Count() != Shortcuts.Count)
            throw new ArgumentException("Saved Core selections are invalid; original settings have been preserved.");

        foreach (var request in C1Selections.Append(C2Selection).Concat(Shortcuts.Select(static shortcut => shortcut.Request)))
            CoreTemplate.Resolve(request, WarmupCooldown);
    }
}

