namespace VenueManager.Harness;

public sealed record TextRun(string Text, float X, float Y, float Right, float Bottom)
{
    public float CenterX => (X + Right) / 2;
    public float CenterY => (Y + Bottom) / 2;
}
