namespace Content.Shared.SimpleStation14.Hologram;

[RegisterComponent]
public sealed class HologramComponent : Component
{
    // // Custom struct that specifies the type of hologram.
    // // First variable is the HoloType enum, second is whether or not it's hardlight.
    // // Defaults to False.
    // [DataField("holoData"), ViewVariables]
    // public HoloData HoloData = new HoloData(HoloType.Projected, false);

    // Current server the Hologram is generated by.
    // Will be the Lightbee if it's a Lightbee Hologram.
    [ViewVariables]
    public EntityUid? LinkedServer;

    // The current projector the Hologram is connected to.
    // Will be the Lightbee if it's a Lightbee Hologram.
    [ViewVariables]
    public EntityUid? CurProjector;

    // Counter before returning the Holo, so you can get through doors.
    [DataField("accumulator")]
    public float Accumulator = 0.5f;
}
