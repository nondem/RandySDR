// Form1 - optional link between the audio squelch and the scanner detection threshold.
// Linked squelch (dBFS) = tracked in-channel noise floor + detection threshold + offset, never closer than
// MinAboveFloorDb to the floor, so noise is always muted. The floor is learned while the scanner is hopping.
namespace RandySDR
{
    public partial class Form1
    {
        private const float MinAboveFloorDb = 3f;
        private const long RetuneIgnoreMs = 400;

        private CheckBox _chkLinkSquelch = null!;
        private NumericUpDown _numSquelchOffset = null!;
        private long _lastRetuneTick;
        private float _noiseFloorDb = float.NaN;

        private bool SquelchLinked => _chkLinkSquelch != null && _chkLinkSquelch.Checked;

        private void BuildSquelchLinkControls()
        {
            _chkLinkSquelch = new CheckBox { Text = "Link squelch to detection threshold", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _numSquelchOffset = NewNumeric(0, -6m, 15m, 1m);
            var tip = new ToolTip();
            tip.SetToolTip(_chkLinkSquelch, "Squelch follows the scan threshold: noise floor + threshold + offset. Run a scan once so the noise floor is learned.");
            tip.SetToolTip(_numSquelchOffset, "Extra dB added to the linked squelch level. Raise it if noise still leaks through.");
            _chkLinkSquelch.CheckedChanged += (_, _) => { ReadScanSettingsFromUi(); ApplyLinkedSquelch(); };
            _numSquelchOffset.ValueChanged += (_, _) => { ReadScanSettingsFromUi(); ApplyLinkedSquelch(); };
        }

        /// <summary>Called every meter tick: learns the noise floor while scanning, then updates the linked squelch.</summary>
        private void UpdateNoiseFloorTracking()
        {
            if (_demodulator == null || _scanner == null || _scanner.State != ScanState.Scanning) return;
            if (Environment.TickCount64 - _lastRetuneTick < RetuneIgnoreMs) return;

            float db = _demodulator.ChannelDb;
            if (db < -75f || db > -5f) return;
            // Falls quickly to quiet channels, rises slowly, so signals do not drag the floor up.
            if (float.IsNaN(_noiseFloorDb)) _noiseFloorDb = db;
            else _noiseFloorDb += (db < _noiseFloorDb ? 0.3f : 0.02f) * (db - _noiseFloorDb);
            ApplyLinkedSquelch();
        }

        private void ApplyLinkedSquelch()
        {
            if (_chkLinkSquelch == null) return;
            bool linked = SquelchLinked;
            trackSquelch.Enabled = !linked && trackVolume.Enabled;
            _btnSquelchAuto.Enabled = !linked && trackVolume.Enabled;
            if (!linked) { UpdateSquelchUi(); return; }

            if (float.IsNaN(_noiseFloorDb))
            {
                lblSquelch.Text = "Squelch: linked (scan to learn)";
                return;
            }

            float db = _noiseFloorDb + (float)EffectiveThresholdDb + (float)_numSquelchOffset.Value;
            db = Math.Clamp(Math.Max(db, _noiseFloorDb + MinAboveFloorDb), FmDemodulator.SquelchMinDb, FmDemodulator.SquelchMaxDb);
            int percent = Math.Clamp(FmDemodulator.DbToSquelchPercent(db), 1, 100);
            if (trackSquelch.Value != percent) trackSquelch.Value = percent;
            if (_demodulator != null) _demodulator.SquelchThresholdDb = db;
            UpdateSquelchUi();
            lblSquelch.Text = $"Squelch: {db:F0} dB (linked)";
        }
    }
}
