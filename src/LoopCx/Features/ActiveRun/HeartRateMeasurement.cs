namespace LoopCx.Features.ActiveRun;

internal static class HeartRateMeasurement
{
    internal static int? Parse(ReadOnlySpan<byte> value)
    {
        if (value.Length < 2) return null;
        var wide = (value[0] & 1) != 0;
        var contactKnown = (value[0] & 4) != 0;
        var contact = (value[0] & 2) != 0;

        if ((contactKnown && !contact) || (wide && value.Length < 3)) return null;
        var bpm = wide ? value[1] | (value[2] << 8) : value[1];

        return bpm > 0 ? bpm : null;
    }
}
