// ============================================================
//  Plan Checker – ICRU Reports 62 & 83
//  Varian Eclipse ESAPI Script (binary / compiled plug-in)
//
//  Checks a loaded treatment plan against the prescribing,
//  recording and reporting guidelines of:
//    • ICRU Report 62 (1999) – 3D-CRT / Photon Beam Therapy
//    • ICRU Report 83 (2010) – IMRT / Photon-Beam Intensity-
//                              Modulated Radiation Therapy
//
//  Requires: Eclipse 15.x / 16.x, .NET 4.8,
//            VMS.TPS.Common.Model.API
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = false)]

namespace VMS.TPS
{
    public class Script
    {
        // ── ICRU dose-volume points ──────────────────────────────────
        // ICRU 83: near-minimum = D98%, near-maximum = D2%, median = D50%
        private const double NEAR_MIN_VOL_PCT  = 98.0;
        private const double NEAR_MAX_VOL_PCT  =  2.0;
        private const double MEDIAN_VOL_PCT    = 50.0;

        // ICRU 62: coverage = D95%, hot-spot = D5%
        private const double COVERAGE_VOL_PCT  = 95.0;
        private const double HOT_SPOT_VOL_PCT  =  5.0;

        // Near-max point dose volume (cc) – used for absolute Dmax estimate
        private const double NEAR_MAX_ABS_CC   = 0.03;

        // ── Tolerance limits (% of prescribed dose) ──────────────────
        // Acceptable dose range: 95 % – 107 % of Rx
        // (ICRU 50/62/83 consensus; clinic may tighten these values)
        private const double LOWER_LIMIT_PCT   = 95.0;
        private const double UPPER_LIMIT_PCT   = 107.0;

        // Acceptable Homogeneity Index (HI ≤ 0.10 is ideal per ICRU 83)
        private const double HI_LIMIT          = 0.10;

        // RTOG Conformity Index acceptable range [0.9, 1.5]
        private const double CI_LOWER          = 0.90;
        private const double CI_UPPER          = 1.50;

        // ─────────────────────────────────────────────────────────────
        public void Execute(ScriptContext context)
        {
            // ── Pre-flight checks ────────────────────────────────────
            PlanSetup plan = context.PlanSetup;
            if (plan == null)
            {
                ShowWarning("No treatment plan is loaded.\nPlease open a plan and try again.");
                return;
            }

            if (!plan.IsDoseValid)
            {
                ShowWarning("Dose has not been calculated for this plan.\nPlease calculate dose first.");
                return;
            }

            DoseValue totalDose = plan.TotalDose;
            if (!totalDose.IsValid() || totalDose.Dose <= 0)
            {
                ShowWarning("No valid prescription dose found for this plan.");
                return;
            }

            StructureSet ss = plan.StructureSet;
            if (ss == null)
            {
                ShowWarning("No structure set is associated with this plan.");
                return;
            }

            // ── Find all PTV structures ──────────────────────────────
            List<Structure> ptvList = ss.Structures
                .Where(s => !s.IsEmpty &&
                           (s.DicomType.Equals("PTV", StringComparison.OrdinalIgnoreCase) ||
                            s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.Id)
                .ToList();

            if (ptvList.Count == 0)
            {
                ShowWarning("No PTV structure found.\n" +
                    "Ensure a structure has DICOM type 'PTV' or an ID starting with 'PTV'.");
                return;
            }

            // ── Locate BODY/EXTERNAL for conformity index ────────────
            Structure body = ss.Structures
                .FirstOrDefault(s => s.DicomType.Equals("EXTERNAL", StringComparison.OrdinalIgnoreCase) ||
                                     s.Id.Equals("BODY", StringComparison.OrdinalIgnoreCase) ||
                                     s.Id.Equals("EXTERNAL", StringComparison.OrdinalIgnoreCase));

            // ── Build report ─────────────────────────────────────────
            double rxDose   = totalDose.Dose;
            DoseValue.DoseUnit unit = totalDose.Unit;
            bool overallPass = true;
            var sb = new StringBuilder();

            AppendHeader(sb);
            sb.AppendLine($"  Patient  : {context.Patient?.Name ?? "—"}");
            sb.AppendLine($"  Plan ID  : {plan.Id}");
            sb.AppendLine($"  Course   : {context.Course?.Id ?? "—"}");
            sb.AppendLine($"  Rx Dose  : {rxDose:F2} {unit}  " +
                          $"({plan.TreatmentPercentage * 100.0:F1} % normalisation)");
            sb.AppendLine($"  Fxs      : {plan.NumberOfFractions}  ×  " +
                          $"{plan.DosePerFraction.Dose:F2} {unit}/fx");
            AppendDivider(sb);

            foreach (Structure ptv in ptvList)
            {
                bool structPass = true;
                sb.AppendLine($"\n┌─  Structure : {ptv.Id}   (Volume = {ptv.Volume:F2} cc)");

                try
                {
                    // ── Retrieve DVH statistics ──────────────────────
                    DoseValue d98  = plan.GetDoseAtVolume(ptv, NEAR_MIN_VOL_PCT,  VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    DoseValue d95  = plan.GetDoseAtVolume(ptv, COVERAGE_VOL_PCT,  VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    DoseValue d50  = plan.GetDoseAtVolume(ptv, MEDIAN_VOL_PCT,    VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    DoseValue d5   = plan.GetDoseAtVolume(ptv, HOT_SPOT_VOL_PCT,  VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    DoseValue d2   = plan.GetDoseAtVolume(ptv, NEAR_MAX_VOL_PCT,  VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    DoseValue dMax = plan.GetDoseAtVolume(ptv, NEAR_MAX_ABS_CC,   VolumePresentation.AbsoluteCm3, DoseValuePresentation.Absolute);

                    // Convert to % of prescription
                    double pD98  = Pct(d98.Dose,  rxDose);
                    double pD95  = Pct(d95.Dose,  rxDose);
                    double pD50  = Pct(d50.Dose,  rxDose);
                    double pD5   = Pct(d5.Dose,   rxDose);
                    double pD2   = Pct(d2.Dose,   rxDose);
                    double pDMax = Pct(dMax.Dose,  rxDose);

                    // ── Homogeneity Index (ICRU 83, §3.5) ───────────
                    // HI = (D2% − D98%) / D50%
                    double hi = (d2.Dose - d98.Dose) / d50.Dose;

                    // ── Coverage: V95% of PTV (volume % receiving ≥ 95% Rx) ──
                    double v95pct = plan.GetVolumeAtDose(
                        ptv,
                        new DoseValue(rxDose * LOWER_LIMIT_PCT / 100.0, unit),
                        VolumePresentation.Relative);

                    // ── Conformity Index ─────────────────────────────
                    // Paddick CI = TVPIV² / (TV × PIV)
                    //   TVPIV = PTV volume ≥ Rx dose (absolute, cc)
                    //   TV    = PTV volume
                    //   PIV   = body volume ≥ Rx dose (absolute, cc)
                    double? ci = CalculatePaddickCI(plan, ptv, body, rxDose, unit);

                    // ════════════════════════════════════════════════
                    //  ICRU REPORT 62  – 3D-CRT
                    // ════════════════════════════════════════════════
                    sb.AppendLine("│");
                    sb.AppendLine("│  ╔═  ICRU Report 62  (3D-CRT / Photon Beam Therapy)  ═╗");

                    // 1. D95% ≥ 95 % Rx  (coverage criterion)
                    bool p1 = pD95 >= LOWER_LIMIT_PCT;
                    structPass &= p1; overallPass &= p1;
                    sb.AppendLine($"│  ║  {Tick(p1)} D95%           = {d95.Dose,8:F2} {unit}  ({pD95,6:F1} %)   [Req ≥ {LOWER_LIMIT_PCT:F0} %]");

                    // 2. D5% ≤ 107 % Rx  (hot-spot criterion)
                    bool p2 = pD5 <= UPPER_LIMIT_PCT;
                    structPass &= p2; overallPass &= p2;
                    sb.AppendLine($"│  ║  {Tick(p2)} D5%            = {d5.Dose,8:F2} {unit}  ({pD5,6:F1} %)   [Req ≤ {UPPER_LIMIT_PCT:F0} %]");

                    // 3. Near-Dmax ≤ 107 % Rx  (0.03 cc point dose)
                    bool p3 = pDMax <= UPPER_LIMIT_PCT;
                    structPass &= p3; overallPass &= p3;
                    sb.AppendLine($"│  ║  {Tick(p3)} Dmax (0.03 cc) = {dMax.Dose,8:F2} {unit}  ({pDMax,6:F1} %)   [Req ≤ {UPPER_LIMIT_PCT:F0} %]");

                    // 4. ICRU Reference Point dose (D50% used as surrogate)
                    //    ICRU 62 §3.3: Ref-Point dose must lie within 95–107 % Rx
                    bool p4 = pD50 >= LOWER_LIMIT_PCT && pD50 <= UPPER_LIMIT_PCT;
                    structPass &= p4; overallPass &= p4;
                    sb.AppendLine($"│  ║  {Tick(p4)} D50% (ICRU Ref)= {d50.Dose,8:F2} {unit}  ({pD50,6:F1} %)   [Req {LOWER_LIMIT_PCT:F0}–{UPPER_LIMIT_PCT:F0} %]");

                    // 5. Coverage index V95%
                    bool p5 = v95pct >= 95.0;
                    structPass &= p5; overallPass &= p5;
                    sb.AppendLine($"│  ║  {Tick(p5)} V{LOWER_LIMIT_PCT:F0}% (coverage) = {v95pct,8:F1} %               [Req ≥ 95 %]");

                    sb.AppendLine("│  ╚═══════════════════════════════════════════════════╝");

                    // ════════════════════════════════════════════════
                    //  ICRU REPORT 83  – IMRT
                    // ════════════════════════════════════════════════
                    sb.AppendLine("│");
                    sb.AppendLine("│  ╔═  ICRU Report 83  (IMRT)  ══════════════════════════╗");

                    // 1. D98% ≥ 95 % Rx  (near-minimum)
                    bool q1 = pD98 >= LOWER_LIMIT_PCT;
                    structPass &= q1; overallPass &= q1;
                    sb.AppendLine($"│  ║  {Tick(q1)} D98% (near-min)= {d98.Dose,8:F2} {unit}  ({pD98,6:F1} %)   [Req ≥ {LOWER_LIMIT_PCT:F0} %]");

                    // 2. D2% ≤ 107 % Rx  (near-maximum)
                    bool q2 = pD2 <= UPPER_LIMIT_PCT;
                    structPass &= q2; overallPass &= q2;
                    sb.AppendLine($"│  ║  {Tick(q2)} D2%  (near-max)= {d2.Dose,8:F2} {unit}  ({pD2,6:F1} %)   [Req ≤ {UPPER_LIMIT_PCT:F0} %]");

                    // 3. D50% ≈ 100 % Rx (median dose)
                    bool q3 = pD50 >= LOWER_LIMIT_PCT && pD50 <= UPPER_LIMIT_PCT;
                    structPass &= q3; overallPass &= q3;
                    sb.AppendLine($"│  ║  {Tick(q3)} D50% (median)  = {d50.Dose,8:F2} {unit}  ({pD50,6:F1} %)   [Req {LOWER_LIMIT_PCT:F0}–{UPPER_LIMIT_PCT:F0} %]");

                    // 4. Homogeneity Index HI ≤ 0.10
                    bool q4 = hi <= HI_LIMIT;
                    structPass &= q4; overallPass &= q4;
                    sb.AppendLine($"│  ║  {Tick(q4)} HI = (D2%-D98%)/D50% = {hi:F3}           [Req ≤ {HI_LIMIT:F2}]");

                    // 5. Paddick Conformity Index
                    if (ci.HasValue)
                    {
                        bool q5 = ci.Value >= CI_LOWER && ci.Value <= CI_UPPER;
                        structPass &= q5; overallPass &= q5;
                        sb.AppendLine($"│  ║  {Tick(q5)} CI  (Paddick)  = {ci.Value:F3}                  [Req {CI_LOWER:F2}–{CI_UPPER:F2}]");
                    }
                    else
                    {
                        sb.AppendLine("│  ║     CI  (Paddick)  = N/A  (BODY/EXTERNAL structure not found)");
                    }

                    sb.AppendLine("│  ╚═══════════════════════════════════════════════════╝");

                    // ── Per-structure summary ────────────────────────
                    sb.AppendLine($"│");
                    sb.AppendLine($"│  Structure result: {(structPass ? "✓ PASS" : "✗ FAIL — see flagged lines above")}");
                }
                catch (Exception ex)
                {
                    overallPass = false;
                    sb.AppendLine($"│  ⚠  Error evaluating {ptv.Id}: {ex.Message}");
                }

                sb.AppendLine("└─────────────────────────────────────────────────────────");
            }

            // ── Overall summary ──────────────────────────────────────
            sb.AppendLine();
            AppendDivider(sb);
            if (overallPass)
                sb.AppendLine("  OVERALL RESULT:  ✓  ALL ICRU 62/83 CRITERIA PASSED");
            else
                sb.AppendLine("  OVERALL RESULT:  ✗  ONE OR MORE CRITERIA NOT MET");
            AppendDivider(sb);
            sb.AppendLine();
            AppendReferences(sb);

            ShowResults(sb.ToString(), overallPass, plan.Id);
        }

        // ─────────────────────────────────────────────────────────────
        //  Conformity Index (Paddick 2000)
        //  CI = TVPIV² / (TV × PIV)
        //
        //  TVPIV – PTV volume receiving ≥ Rx dose  (cc)
        //  TV    – total PTV volume                 (cc)
        //  PIV   – body volume receiving ≥ Rx dose  (cc)
        // ─────────────────────────────────────────────────────────────
        private static double? CalculatePaddickCI(
            PlanSetup plan, Structure ptv, Structure body,
            double rxDose, DoseValue.DoseUnit unit)
        {
            try
            {
                var rxDoseValue = new DoseValue(rxDose, unit);

                double tvpiv = plan.GetVolumeAtDose(ptv, rxDoseValue, VolumePresentation.AbsoluteCm3);
                double tv    = ptv.Volume;

                if (body == null || body.IsEmpty)
                    return null;

                double piv = plan.GetVolumeAtDose(body, rxDoseValue, VolumePresentation.AbsoluteCm3);

                if (tv <= 0 || piv <= 0)
                    return null;

                return (tvpiv * tvpiv) / (tv * piv);
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────
        private static double Pct(double dose, double rxDose) =>
            rxDose > 0 ? dose / rxDose * 100.0 : 0.0;

        private static string Tick(bool pass) => pass ? "✓" : "✗";

        private static void ShowWarning(string message) =>
            MessageBox.Show(message, "Plan Checker ICRU 62/83",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void AppendHeader(StringBuilder sb)
        {
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine("          PLAN CHECKER  –  ICRU REPORTS 62 & 83");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
        }

        private static void AppendDivider(StringBuilder sb) =>
            sb.AppendLine("──────────────────────────────────────────────────────────────");

        private static void AppendReferences(StringBuilder sb)
        {
            sb.AppendLine("References");
            sb.AppendLine("──────────");
            sb.AppendLine("  ICRU Report 62 (1999): Prescribing, Recording and Reporting");
            sb.AppendLine("    Photon Beam Therapy – Supplement to ICRU Report 50.");
            sb.AppendLine("    International Commission on Radiation Units and Measurements.");
            sb.AppendLine();
            sb.AppendLine("  ICRU Report 83 (2010): Prescribing, Recording, and Reporting");
            sb.AppendLine("    Photon-Beam Intensity-Modulated Radiation Therapy (IMRT).");
            sb.AppendLine("    J ICRU 10(1). doi:10.1093/jicru/ndq002");
            sb.AppendLine();
            sb.AppendLine("  Paddick I (2000): A simple scoring ratio to index the");
            sb.AppendLine("    conformity of radiosurgical treatment plans.");
            sb.AppendLine("    J Neurosurg 93 Suppl 3:219–222.");
        }

        private static void ShowResults(string report, bool pass, string planId)
        {
            var textBox = new TextBox
            {
                Text = report,
                FontFamily = new FontFamily("Courier New"),
                FontSize = 12,
                IsReadOnly = true,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = pass ? Brushes.Honeydew : Brushes.MistyRose,
                Padding = new Thickness(10)
            };

            var window = new Window
            {
                Title = $"Plan Checker – ICRU 62/83  [{planId}]",
                Width  = 750,
                Height = 660,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = textBox
            };

            window.ShowDialog();
        }
    }
}
