namespace RandySDR
{
    /// <summary>Demodulation modes selectable in the UI. Add a new mode here, then handle it in FmDemodulator.Demodulate.</summary>
    public enum DemodulationMode
    {
        WideFm,
        NarrowFm,
        Am,
        Usb,
        Lsb,
        Raw,
        Cw
    }
}
