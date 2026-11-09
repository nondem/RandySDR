# Panel Resize Feature - Test Plan

> **Historical and inaccurate:** This plan describes an earlier broken implementation. Use the automated `Tests\PanelLayoutChecks` and `Tests\PanelResizeChecks` checks and the current behavior described in `ARCHITECTURE.md`.

## Objective
Verify the panel resize functionality works correctly across all phases of implementation.

---

## Test Scenarios

### T1: Toggle Mode Activation
**Goal:** Verify the custom resize toggle button works correctly.

**Steps:**
1. Launch RandySDR application
2. Observe toolbar contains reset button, info label, and NEW "Custom resize: OFF" button
3. Click the toggle button once
4. Button should change to "Custom resize: ON" with green background
5. Resize handle cursors should appear on all panels when hovering edges

**Expected Result:**
- ✅ Toggle button appears in toolbar between reset button and info label
- ✅ Text changes from "Custom resize: OFF" → "Custom resize: ON"
- ✅ Button background changes from gray (#32465A) to green (#1E7846)
- ✅ Cursor changes to VSplit (top/bottom edges) and HSplit (left edges of narrow cards)

**Pass Criteria:** All 5 points above confirmed

---

### T2: Height Resize Operation
**Goal:** Verify panels can be resized vertically with proper constraints.

**Steps:**
1. Activate custom resize mode (toggle button to ON state)
2. Hover over TOP edge of any panel until cursor changes to VSplit arrow
3. Click and drag downward slowly
4. Observe the panel stretching in real-time
5. Continue dragging past 100px increment
6. Release mouse button

**Expected Result:**
- ✅ Cursor shows vertical split (↕) at top edge
- ✅ Panel visibly stretches as mouse is dragged
- ✅ Snap-to-grid effect: panel "jumps" to nearest 10px when close
- ✅ No error or crash during resize operation
- ✅ After release, panel height is snapped to valid increment

**Pass Criteria:** All 5 points confirmed

---

### T3: Width Resize Operation (Narrow Cards Only)
**Goal:** Verify narrow cards can be resized horizontally.

**Steps:**
1. Activate custom resize mode
2. Identify a non-wide panel (not "tuning", "gain", or "spectrum")
3. Hover over LEFT edge until cursor changes to HSplit arrow
4. Click and drag rightward
5. Observe panel stretching horizontally
6. Try dragging left beyond 450px minimum

**Expected Result:**
- ✅ Cursor shows horizontal split (↔) at left edge only for narrow cards
- ✅ Panel stretches as mouse is dragged
- ✅ Snap-to-grid effect visible during drag
- ✅ Minimum width constraint enforced (~450px)
- ✅ Wide panels ("tuning", "gain", "spectrum") do NOT show HSplit cursor

**Pass Criteria:** All 5 points confirmed

---

### T4: Height Constraint Enforcement
**Goal:** Verify minimum height limits are respected.

**Steps:**
1. Activate custom resize mode
2. Hover over TOP edge of any panel
3. Drag upward as much as possible
4. Attempt to resize smaller than ~60px total height

**Expected Result:**
- ✅ Panel resizes normally while above minimum threshold
- ✅ At minimum height, further dragging is prevented
- ✅ Panel "locks" at approximately header height + 20px (~68px for default font)
- ✅ No visual glitching or layout corruption at constraint boundary

**Pass Criteria:** All 4 points confirmed

---

### T5: Persistence - Save Functionality
**Goal:** Verify custom panel heights are saved on window close.

**Steps:**
1. Launch application (first run after clean install)
2. Activate custom resize mode
3. Resize multiple panels to unique heights:
   - "device" card: 180px
   - "tuning" card: 160px
   - "demodulation" card: 140px
4. Close the application window completely

**Expected Result:**
- ✅ File created at `%AppData%\RandySDR\panel-sizes.json`
- ✅ JSON file contains entries in format: `"id:height"` for each resized panel
- ✅ File contents show correct heights matching user input (±2px tolerance)
- ✅ No error dialogs during close operation

**Pass Criteria:** All 4 points confirmed

---

### T6: Persistence - Load Functionality
**Goal:** Verify saved panel heights are restored on startup.

**Steps:**
1. From previous test, application has saved custom heights
2. Close and restart the application fresh
3. Observe initial layout after window appears
4. Compare panel heights to previously set values
5. Hover over edges to verify resize handles reappear

**Expected Result:**
- ✅ Panels restore to their saved heights immediately upon launch
- ✅ "device" card shows ~180px height (±5px tolerance)
- ✅ "tuning" card shows ~160px height (±5px tolerance)
- ✅ "demodulation" card shows ~140px height (±5px tolerance)
- ✅ Resize handles reappear with same visual indicators as before

**Pass Criteria:** All 5 points confirmed

---

### T7: Reset Button Functionality
**Goal:** Verify the reset button correctly restores default heights.

**Steps:**
1. Activate custom resize mode and resize panels to various heights
2. Click the "Reset panel order" button in toolbar
3. Observe panel heights change

**Expected Result:**
- ✅ Click triggers immediate layout recalculation
- ✅ All panels revert to their original designer-specified heights
- ✅ No loss of current panel ordering (cards stay in user's preferred positions)
- ✅ Resize handles reappear after reset, ready for further customization

**Pass Criteria:** All 4 points confirmed

---

### T8: Grid Snap Precision
**Goal:** Verify snap-to-grid logic works with correct precision.

**Steps:**
1. Activate custom resize mode
2. Resize a panel incrementally while measuring the final height
3. Test at various base sizes (starting at 50px, 100px, 150px)
4. Drag to heights that would result in non-multiples of 10:
   - 57px → should snap to 60px
   - 53px → should snap to 50px
   - 124px → should snap to 120px
   - 126px → should snap to 130px

**Expected Result:**
- ✅ Panels always end at multiples of 10px (±2px tolerance)
- ✅ Snap distance threshold is approximately ±4px from grid line
- ✅ No jittering or oscillation during snap events
- ✅ Visual feedback shows snap direction (up/down) during drag

**Pass Criteria:** All 4 points confirmed

---

### T9: Mode Switching Behavior
**Goal:** Verify switching between auto-fit and custom modes works correctly.

**Steps:**
1. Resize panels in custom mode to various heights
2. Toggle back to "OFF" (auto-fit) mode
3. Observe panels return to original dimensions
4. Toggle back to "ON" (custom) mode
5. Verify resize handles reappear

**Expected Result:**
- ✅ Clicking OFF causes all panels to snap to original heights immediately
- ✅ Toggle button text changes correctly: "ON" → "OFF"
- ✅ Button background changes: green → gray on OFF
- ✅ Resize handle cursors disappear in OFF mode
- ✅ Clicking ON restores all previous customizations
- ✅ Resize handle cursors reappear in ON mode

**Pass Criteria:** All 6 points confirmed

---

### T10: Wide Card Protection
**Goal:** Verify wide cards cannot be resized horizontally.

**Steps:**
1. Activate custom resize mode
2. Hover over LEFT edge of "tuning" card (wide card)
3. Hover over LEFT edge of "demodulation" card (narrow card)
4. Compare cursor types at same position

**Expected Result:**
- ✅ Wide cards ("tuning", "gain", "spectrum") show NO HSplit cursor on left edge
- ✅ Wide cards still show VSplit cursor for height resizing
- ✅ Narrow cards show BOTH VSplit (top) and HSplit (left) cursors
- ✅ No error or crash when attempting to resize wide card horizontally

**Pass Criteria:** All 4 points confirmed

---

## Execution Instructions

Run tests in order T1→T10. Document any failures with:
- Exact steps taken
- Expected vs actual behavior
- Screenshots (if possible)
- Application version and environment details

## Test Environment

| Component | Value |
|-----------|-------|
| OS | Windows 10/11 |
| .NET Runtime | .NET 10.0 |
| Application | RandySDR v1.0 |
| Test Date | [Fill in] |
| Tester | [Fill in] |

---

## Pass/Fail Summary

| Test ID | Description | Status (✓/✗) | Notes |
|---------|-------------|--------------|-------|
| T1 | Toggle Mode Activation |  |  |
| T2 | Height Resize Operation |  |  |
| T3 | Width Resize (Narrow Cards) |  |  |
| T4 | Height Constraint Enforcement |  |  |
| T5 | Persistence - Save |  |  |
| T6 | Persistence - Load |  |  |
| T7 | Reset Button Functionality |  |  |
| T8 | Grid Snap Precision |  |  |
| T9 | Mode Switching Behavior |  |  |
| T10 | Wide Card Protection |  |  |

**Overall Result:** □ PASS (all tests pass) / □ FAIL (one or more tests fail)

---

## Sign-off

Tester Signature: _________________ Date: ___________
