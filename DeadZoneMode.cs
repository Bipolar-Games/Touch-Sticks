namespace Bipolar.TouchSticks
{
    /// <summary>How <see cref="VirtualJoystick"/> treats the dead zone when the output is not normalized.</summary>
    public enum DeadZoneMode
    {
        /// <summary>Deflection below the dead zone gives zero, above it the raw deflection. The value jumps from 0 to the dead zone size.</summary>
        Cut,

        /// <summary>Deflection below the dead zone gives zero, above it the value grows smoothly from 0 at the dead zone edge to 1 at full deflection.</summary>
        Rescale,
    }
}
