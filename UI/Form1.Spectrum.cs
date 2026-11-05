// Form1 - spectrum/waterfall: refresh timer, zoom, range, reference level.
namespace RandySDR
{
    public partial class Form1
    {
        private void SpectrumTimer_Tick(object? sender, EventArgs e)
        {
            if (_spectrum == null || !chkSpectrumEnabled.Checked || _scanMuted)
                return;

            var iq = _lastIqData;
            if (iq == null || iq.Length < _spectrum.FftSize * 2)
                return;

            try
            {
                int sampleRateHz = (int)(numSampleRate.Value * 1_000_000);
                var db = _spectrum.ProcessIQ(iq);

                float offsetHz = _spectrum.GetFrequencyOffset(sampleRateHz);
                lblFreqOffset.Text = $"Peak offset: {offsetHz / 1000:+0.0;-0.0;0.0} kHz";
                lblFreqOffset.ForeColor = Math.Abs(offsetHz) > 10000 ? Color.Red
                    : Math.Abs(offsetHz) > 2000 ? Color.Orange : Color.Green;

                int passbandHz = GetDemodulationMode() switch
                {
                    DemodulationMode.NarrowFm => 12_500,
                    DemodulationMode.Am => 10_000,
                    DemodulationMode.Usb or DemodulationMode.Lsb => 3_000,
                    DemodulationMode.Cw => 500,
                    _ => 200_000
                };
                picSpectrum.SetSpectrum(db, (double)numFrequency.Value * 1e6, sampleRateHz, passbandHz);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Spectrum error: {ex.Message}");
            }
        }

        private void chkSpecAuto_CheckedChanged(object? sender, EventArgs e)
        {
            bool manual = !chkSpecAuto.Checked;
            trackSpecRange.Enabled = manual;
            trackSpecRef.Enabled = manual;
            if (manual)
            {
                // Start from what auto-range was showing
                int top = (int)Math.Clamp(Math.Round(picSpectrum.CurrentMaxDb), trackSpecRef.Minimum, trackSpecRef.Maximum);
                int span = (int)Math.Clamp(Math.Round(picSpectrum.CurrentMaxDb - picSpectrum.CurrentMinDb), trackSpecRange.Minimum, trackSpecRange.Maximum);
                trackSpecRef.Value = top;
                trackSpecRange.Value = span;
                picSpectrum.RefDb = top;
                picSpectrum.RangeDb = span;
                lblSpecRef.Text = $"Ref: {top} dB";
                lblSpecRange.Text = $"Range: {span} dB";
            }
            picSpectrum.AutoRange = chkSpecAuto.Checked;
        }

        private void trackSpecZoom_Scroll(object? sender, EventArgs e)
        {
            // 0..50 -> 1x..32x (logarithmic)
            double z = Math.Pow(2.0, trackSpecZoom.Value / 10.0);
            picSpectrum.Zoom = z;
            lblSpecZoom.Text = $"Zoom: {z:0.0}x";
        }

        private void trackSpecRange_Scroll(object? sender, EventArgs e)
        {
            picSpectrum.RangeDb = trackSpecRange.Value;
            lblSpecRange.Text = $"Range: {trackSpecRange.Value} dB";
        }

        private void trackSpecRef_Scroll(object? sender, EventArgs e)
        {
            picSpectrum.RefDb = trackSpecRef.Value;
            lblSpecRef.Text = $"Ref: {trackSpecRef.Value} dB";
        }

        private void chkSpectrumEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSpectrumEnabled.Checked)
            {
                if (_sdr != null && btnClose.Enabled)
                {
                    _spectrum = new SpectrumAnalyzer(4096);
                    _spectrumTimer.Start();
                }
            }
            else
            {
                _spectrum = null;
                _spectrumTimer.Stop();
                picSpectrum.Clear();
                lblFreqOffset.Text = "Offset: disabled";
                lblFreqOffset.ForeColor = Color.Gray;
            }
        }
    }
}
