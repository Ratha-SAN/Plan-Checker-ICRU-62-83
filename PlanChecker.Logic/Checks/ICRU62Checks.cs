using System;
using System.Collections.Generic;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Checks
{
    /// <summary>
    /// Plan quality checks derived from ICRU Report 62
    /// <em>Prescribing, Recording and Reporting Photon Beam Therapy</em>
    /// (Supplement to ICRU Report 50).
    /// <para>
    /// Key criteria:
    /// <list type="bullet">
    ///   <item>PTV, CTV and GTV structures should be defined.</item>
    ///   <item>D95% ≥ 95 % of prescription dose.</item>
    ///   <item>PTV Dmax ≤ 107 % of prescription dose.</item>
    ///   <item>ICRU reference-point dose between 95 % and 107 % of Rx.</item>
    ///   <item>Global hot-spot (Dmax outside PTV) ≤ 110 % of Rx.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ICRU62Checks
    {
        // ── ICRU 62 / 50 dose criteria ─────────────────────────────────────────
        /// <summary>D95% must be ≥ this percentage of the prescription dose.</summary>
        public const double MinD95CoveragePercent = 95.0;

        /// <summary>PTV Dmax must be ≤ this percentage of the prescription dose.</summary>
        public const double MaxPTVDosePercent = 107.0;

        /// <summary>Global hot-spot limit as percentage of the prescription dose.</summary>
        public const double MaxHotSpotPercent = 110.0;

        /// <summary>ICRU reference-point dose lower bound (% of Rx).</summary>
        public const double MinRefPointDosePercent = 95.0;

        /// <summary>ICRU reference-point dose upper bound (% of Rx).</summary>
        public const double MaxRefPointDosePercent = 107.0;

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Runs all ICRU 62 checks and returns the list of results.
        /// </summary>
        /// <param name="ptvData">DVH data for the PTV structure.</param>
        /// <param name="prescriptionDoseGy">Total prescription dose in Gy.</param>
        /// <param name="hasPTV">Whether a PTV structure exists in the plan.</param>
        /// <param name="hasCTV">Whether a CTV structure exists in the plan.</param>
        /// <param name="hasGTV">Whether a GTV structure exists in the plan.</param>
        /// <param name="globalMaxDoseGy">Global maximum dose (Dmax) in Gy. Pass <see cref="double.NaN"/> to skip.</param>
        /// <param name="referencePointDoseGy">Dose at the ICRU reference point in Gy (optional).</param>
        public static List<CheckResult> RunChecks(
            StructureDvhData? ptvData,
            double prescriptionDoseGy,
            bool hasPTV,
            bool hasCTV,
            bool hasGTV,
            double globalMaxDoseGy = double.NaN,
            double? referencePointDoseGy = null)
        {
            var results = new List<CheckResult>();

            // 1. Target-volume structure checks
            results.Add(CheckTargetVolumes(hasPTV, hasCTV, hasGTV));

            if (ptvData == null || prescriptionDoseGy <= 0)
                return results;

            // 2. PTV dose-coverage checks
            results.Add(CheckPTVD95Coverage(ptvData, prescriptionDoseGy));
            results.Add(CheckPTVMinDose(ptvData, prescriptionDoseGy));
            results.Add(CheckPTVMaxDose(ptvData, prescriptionDoseGy));

            // 3. ICRU reference-point dose
            if (referencePointDoseGy.HasValue)
                results.Add(CheckICRUReferencePoint(referencePointDoseGy.Value, prescriptionDoseGy));

            // 4. Global hot-spot
            if (!double.IsNaN(globalMaxDoseGy))
                results.Add(CheckGlobalHotSpot(globalMaxDoseGy, prescriptionDoseGy));

            return results;
        }

        // ── Individual checks ──────────────────────────────────────────────────

        /// <summary>
        /// Checks that PTV (required), CTV and GTV (recommended) structures exist.
        /// </summary>
        public static CheckResult CheckTargetVolumes(bool hasPTV, bool hasCTV, bool hasGTV)
        {
            if (!hasPTV)
                return new CheckResult
                {
                    Name = "Target Volume Structures (GTV / CTV / PTV)",
                    Standard = "ICRU 62",
                    Status = CheckStatus.Fail,
                    Message = "PTV structure not found – required per ICRU 62."
                };

            var parts = new System.Text.StringBuilder("PTV found.");
            if (hasCTV) parts.Append(" CTV found."); else parts.Append(" CTV not found (recommended per ICRU 62).");
            if (hasGTV) parts.Append(" GTV found."); else parts.Append(" GTV not found (recommended per ICRU 62).");

            return new CheckResult
            {
                Name = "Target Volume Structures (GTV / CTV / PTV)",
                Standard = "ICRU 62",
                Status = (hasCTV || hasGTV) ? CheckStatus.Pass : CheckStatus.Warning,
                Message = parts.ToString()
            };
        }

        /// <summary>Checks that D95% ≥ 95 % of prescription dose (ICRU 62).</summary>
        public static CheckResult CheckPTVD95Coverage(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double d95 = DvhCalculator.GetDoseAtVolume(ptvData, 95.0);

            if (double.IsNaN(d95))
                return new CheckResult
                {
                    Name = "PTV D95% Coverage",
                    Standard = "ICRU 62",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate PTV D95% – DVH data unavailable."
                };

            double pct = (d95 / prescriptionDoseGy) * 100.0;
            bool pass = pct >= MinD95CoveragePercent;

            return new CheckResult
            {
                Name = "PTV D95% Coverage",
                Standard = "ICRU 62",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(pct, 1),
                Limit = MinD95CoveragePercent,
                Unit = "% of Rx",
                Message = pass
                    ? $"PTV D95% = {pct:F1}% of Rx (criterion: ≥ {MinD95CoveragePercent}%)"
                    : $"PTV D95% = {pct:F1}% of Rx (criterion: ≥ {MinD95CoveragePercent}% – FAIL)"
            };
        }

        /// <summary>Checks PTV minimum dose (informational; soft threshold at 90 % of Rx).</summary>
        public static CheckResult CheckPTVMinDose(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double dmin = ptvData.MinDoseGy;
            double pct = (dmin / prescriptionDoseGy) * 100.0;
            bool pass = pct >= 90.0;

            return new CheckResult
            {
                Name = "PTV Minimum Dose (Dmin)",
                Standard = "ICRU 62",
                Status = pass ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(pct, 1),
                Limit = 90.0,
                Unit = "% of Rx",
                Message = pass
                    ? $"PTV Dmin = {pct:F1}% of Rx (≥ 90%)"
                    : $"PTV Dmin = {pct:F1}% of Rx (< 90% – review cold region)"
            };
        }

        /// <summary>Checks that PTV Dmax ≤ 107 % of prescription dose (ICRU 62).</summary>
        public static CheckResult CheckPTVMaxDose(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double dmax = ptvData.MaxDoseGy;
            double pct = (dmax / prescriptionDoseGy) * 100.0;
            bool pass = pct <= MaxPTVDosePercent;

            return new CheckResult
            {
                Name = "PTV Maximum Dose (Dmax)",
                Standard = "ICRU 62",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(pct, 1),
                Limit = MaxPTVDosePercent,
                Unit = "% of Rx",
                Message = pass
                    ? $"PTV Dmax = {pct:F1}% of Rx (criterion: ≤ {MaxPTVDosePercent}%)"
                    : $"PTV Dmax = {pct:F1}% of Rx (criterion: ≤ {MaxPTVDosePercent}% – FAIL)"
            };
        }

        /// <summary>
        /// Checks the ICRU reference-point dose is within 95–107 % of Rx.
        /// </summary>
        public static CheckResult CheckICRUReferencePoint(double referencePointDoseGy, double prescriptionDoseGy)
        {
            double pct = (referencePointDoseGy / prescriptionDoseGy) * 100.0;
            bool pass = pct >= MinRefPointDosePercent && pct <= MaxRefPointDosePercent;

            return new CheckResult
            {
                Name = "ICRU Reference-Point Dose",
                Standard = "ICRU 62",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(pct, 1),
                Unit = "% of Rx",
                Message = pass
                    ? $"ICRU ref. point = {pct:F1}% of Rx (criterion: {MinRefPointDosePercent}–{MaxRefPointDosePercent}%)"
                    : $"ICRU ref. point = {pct:F1}% of Rx (outside {MinRefPointDosePercent}–{MaxRefPointDosePercent}% – FAIL)"
            };
        }

        /// <summary>Checks the global hot-spot (Dmax overall) ≤ 110 % of Rx.</summary>
        public static CheckResult CheckGlobalHotSpot(double maxDoseGy, double prescriptionDoseGy)
        {
            double pct = (maxDoseGy / prescriptionDoseGy) * 100.0;
            bool pass = pct <= MaxHotSpotPercent;

            return new CheckResult
            {
                Name = "Global Maximum Dose (Hot Spot)",
                Standard = "ICRU 62",
                Status = pass ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(pct, 1),
                Limit = MaxHotSpotPercent,
                Unit = "% of Rx",
                Message = pass
                    ? $"Global Dmax = {pct:F1}% of Rx (criterion: ≤ {MaxHotSpotPercent}%)"
                    : $"Global Dmax = {pct:F1}% of Rx (> {MaxHotSpotPercent}% – review hot-spot location)"
            };
        }
    }
}
