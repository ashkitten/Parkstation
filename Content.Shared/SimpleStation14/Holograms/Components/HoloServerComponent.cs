namespace Content.Shared.SimpleStation14.Holograms;

[RegisterComponent]
public sealed class HologramServerComponent : Component
{
    [ViewVariables]
    public EntityUid? LinkedHologram;
}
