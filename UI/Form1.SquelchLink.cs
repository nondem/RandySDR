// Form1 - optional link between the audio squelch and the scanner detection threshold.
// Linked squelch (dBFS) = tracked in-channel noise floor + detection threshold + offset, never closer than
// MinAboveFloorDb to the floor where the supported range permits it. The floor is learned on quiet settled hops.
namespace RandySDR
{
    public partial class Form1
    {
        /// <summary>Minimum offset above the noise floor for squelch. Higher values prevent noise but may mute very weak signals.</summary>
        private const float MinAboveFloorDb = 2f;
        private CheckBox _chkLinkSquelch = null!;
        private NumericUpDown _numSquelchOffset = null!;
        private Label _lblSquelchLinkStatus = null!;
        private long _lastRetuneTick;
        private int _squelchCalibrationVersion;
        private float _noiseFloorDb = float.NaN;
        private int _manualSquelchValue;

        private bool SquelchLinked => _chkLinkSquelch != null && _chkLinkSquelch.Checked;

        private void BuildSquelchLinkControls()
        {
            _chkLinkSquelch = new CheckBox { Text = "Link squelch to detection threshold", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _numSquelchOffset = NewNumeric(0, -6m, 15m, 1m);
            _lblSquelchLinkStatus = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            var tip = new ToolTip();
            tip.SetToolTip(_chkLinkSquelch, "Uses a quiet scan hop to estimate in-channel noise. Until then, the manual squelch remains in effect. Unlink to restore the manual setting.");
            tip.SetToolTip(_numSquelchOffset, "Added to the detection threshold for audio squelch. Positive values reduce noise but can mute weak detected signals.");
            _chkLinkSquelch.CheckedChanged += (_, _) =>
            {
                if (_loadingScanUi) return;
                if (SquelchLinked) _manualSquelchValue = trackSquelch.Value;
                else trackSquelch.Value = _manualSquelchValue;
                ReadScanSettingsFromUi();
            };
            _numSquelchOffset.ValueChanged += (_, _) => ReadScanSettingsFromUi();
        }

        private void OnQuietChannelMeasured(float db)
        {
            if (float.IsNaN(_noiseFloorDb)) _noiseFloorDb = db;
            else _noiseFloorDb += (db < _noiseFloorDb ? 0.3f : 0.02f) * (db - _noiseFloorDb);
            ApplyLinkedSquelch();
        }

        private void InvalidateSquelchCalibration()
        {
            _squelchCalibrationVersion++;
            if (float.IsNaN(_noiseFloorDb)) return;
            _noiseFloorDb = float.NaN;
            if (_chkLinkSquelch != null) ApplyLinkedSquelch();
        }

        private float? CurrentSquelchThreshold()
        {
            if (SquelchLinked && float.IsFinite(_noiseFloorDb))
            {
                float db = _noiseFloorDb + (float)EffectiveThresholdDb + (float)_numSquelchOffset.Value;
                return Math.Clamp(Math.Max(db, _noiseFloorDb + MinAboveFloorDb),
                    FmDemodulator.SquelchMinDb, FmDemodulator.SquelchMaxDb);
            }
            return _manualSquelchValue == 0 ? null : FmDemodulator.SquelchPercentToDb(_manualSquelchValue);
        }

        private void ApplyLinkedSquelch()
        {
            if (_chkLinkSquelch == null || _loadingScanUi) return;
            bool linked = SquelchLinked;
            trackSquelch.Enabled = !linked && !_squelchAutoSampling && trackVolume.Enabled;
            _btnSquelchAuto.Enabled = !linked && !_squelchAutoSampling && btnStop.Enabled
                && (_scanner == null || _scanner.State == ScanState.Idle);
            _numSquelchOffset.Enabled = linked;
            float? threshold = CurrentSquelchThreshold();
            int percent = threshold is float db ? Math.Clamp(FmDemodulator.DbToSquelchPercent(db), 1, 100) : 0;
            if (trackSquelch.Value != percent) trackSquelch.Value = percent;
            if (_demodulator != null) _demodulator.SquelchThresholdDb = threshold;
            _lblSquelchLinkStatus.Text = !linked
                ? "Manual audio squelch"
                : float.IsNaN(_noiseFloorDb)
                    ? "Waiting for a quiet scan hop; manual squelch remains in use."
                    : $"Linked: floor {_noiseFloorDb:F1} dBFS, audio squelch {threshold:F1} dBFS";
            UpdateSquelchUi();
        }
    }
}
