// OBSOLETE GAMEPLAY BASELINE: original-FreeTrain prototype compatibility/reference only.
// Canonical game logic belongs in FreeTrain.Online.Simulation and follows stable ExAV.
namespace FreeTrain.Online.Simulation.Prototype;

// Domain coordinates only. Projection and camera state belong to the browser.
public readonly record struct GridPosition(int X, int Y, int Z);

public readonly record struct ManualInput(bool Pass, bool Depart, bool Reverse, bool Left, bool Right);
