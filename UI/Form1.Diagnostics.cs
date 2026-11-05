// Form1 - diagnostics report.
namespace RandySDR
{
    public partial class Form1
    {
        private void btnDiagnostics_Click(object sender, EventArgs e)
        {
            if (_demodulator == null)
            {
                MessageBox.Show("Please OPEN and START the device first!", "Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string diagnostics = FrequencyDiagnostics.DiagnoseFrequencyIssue(
                _demodulator.SignalLevel,
                (int)numPpm.Value,
                (double)numFrequency.Value,
                (double)numSampleRate.Value);

            // Add spectrum info if available
            if (_spectrum != null && _lastIqData != null)
            {
                var spectrum = _spectrum.ProcessIQ(_lastIqData);
                float offset = _spectrum.GetFrequencyOffset((int)(numSampleRate.Value * 1_000_000));

                diagnostics += $"\n=== SPECTRUM ANALYSIS ===\n";
                diagnostics += $"Peak frequency offset: {offset / 1000:F1} kHz\n";
                
                if (Math.Abs(offset) > 5000) // More than 5 kHz offset
                {
                    diagnostics += $"\n⚠️ FREQUENCY OFFSET DETECTED!";
                    diagnostics += $"\n\nYour RTL-SDR may have a PPM calibration error.";
                    diagnostics += $"\n\nTry these solutions:";
                    diagnostics += $"\n• Adjust PPM slider to reduce offset toward 0 kHz";
                    diagnostics += $"\n• Recommended: Start at -50 to +50 ppm";
                    diagnostics += $"\n• Fine-tune while monitoring signal strength";
                    diagnostics += $"\n\nCurrent setting: {numPpm.Value} ppm";
                    
                    // Calculate suggested PPM adjustment
                    double suggestedPpm = (double)numPpm.Value - (offset / 1000.0) / ((double)numFrequency.Value);
                    diagnostics += $"\n\nSuggested PPM: {suggestedPpm:F1} ppm";
                }
                else
                {
                    diagnostics += $"\n✓ Frequency offset is within acceptable range";
                }

                diagnostics += $"\n\nIf signal is weak at current frequency:";
                diagnostics += $"\n• Try tuning to {(double)numFrequency.Value + offset / 1_000_000:F3} MHz";
                diagnostics += $"\n• Or adjust PPM calibration";
            }

            diagnostics += $"\n\n=== CURRENT SETTINGS ===\n";
            diagnostics += $"Sample Rate: {numSampleRate.Value:F1} MSPS";
            if (numSampleRate.Value != 2.4m)
                diagnostics += " ⚠️ (Use 2.4 MSPS!)";
            diagnostics += $"\nTuner AGC: {(chkTunerAGC.Checked ? "ON" : "OFF")}";
            if (!chkTunerAGC.Checked)
                diagnostics += $" (Gain: {trackTunerGain.Value / 10.0:F1} dB)";
            diagnostics += $"\nRTL AGC: {(chkRtlAGC.Checked ? "ON ⚠️ (try OFF)" : "OFF ✓")}";
            diagnostics += $"\nPPM Correction: {numPpm.Value} ppm";
            diagnostics += $"\nVolume: {trackVolume.Value}%";
            diagnostics += trackSquelch.Value == 0 ? "\nSquelch: Off" : $"\nSquelch: {trackSquelch.Value}% ({FmDemodulator.SquelchPercentToDb(trackSquelch.Value):F0} dB), in-channel {_demodulator?.ChannelDb:F0} dB, {(_demodulator?.SquelchOpen == true ? "OPEN" : "CLOSED")}";
            diagnostics += $"\nDe-emphasis: {(chkDeEmphasis.Checked ? "ON ✓" : "OFF")}";

            MessageBox.Show(diagnostics, "Frequency Diagnostics",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
