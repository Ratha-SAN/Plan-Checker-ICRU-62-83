using System;
using System.Collections.Generic;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Checks
{
    /// <summary>
    /// Plan quality checks derived from ICRU Report 83
    /// <em>Prescribing, Recording and Reporting Intensity-Modulated
    /// Photon-Beam Radiation Therapy (IMRT)</em>.
    /// <para>
    /// Key metrics (all referenced to the PTV):
    /// <list type="bullet">
    ///   <item>D98% (near-minimum) ≥ 95 % of Rx.</item>
    ///   <item>D2%  (near-maximum) ≤ 107 % of Rx.</item>
    ///   <item>D50% (median dose)  within 95–107 % of Rx.</item>
    ///   <item>Mean dose within 95–107 % of Rx.</item>
    ///   <item>Homogeneity Index HI = (D2% − D98%) / D50%  (lower is better; &lt; 0.1 excellent, &lt; 0.2 acceptable).</item>
    ///   <item>Conformity Index CI = V_Rx / V_PTV  (ideal ≈ 1; acceptable 0.8–1.2).</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ICRU83Checks
    {
        // ── ICRU 83 dose criteria ──────────────────────────────────────────────
        /// <summary>D98% must be ≥ this percentage of the prescription dose (near-minimum dose).</summary>
        public const double MinD98Percent = 95.0;

        /// <summary>D2% must be ≤ this percentage of the prescription dose (near-maximum dose).</summary>
        public const double MaxD2Percent = 107.0;

        /// <summary>Lower bound for D50% (median dose) as percentage of Rx.</summary>
        public const double MinD50Percent = 95.0;

        /// <summary>Upper bound for D50% (median dose) as percentage of Rx.</summary>
        public const double MaxD50Percent = 107.0;

        /// <summary>HI below this threshold is considered excellent.</summary>
        public const double MaxHIExcellent = 0.10;

        /// <summary>HI below this threshold is considered acceptable.</summary>
        public const double MaxHIAcceptable = 0.20;

        /// <summary>CI lower bound for a well-conforming plan.</summary>
        public const double MinCIGood = 0.90;

        /// <summary>CI upper bound for a well-conforming plan.</summary>
        public const double MaxCIGood = 1.10;

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Runs all ICRU 83 checks and returns the list of results.
        /// </summary>
        /// <param name="ptvData">DVH data for the PTV structure.</param>
        /// <param name="prescriptionDoseGy">Total prescription dose in Gy.</param>
        /// <param name="volumeAtRxDoseCC">
        /// Volume (cc) of the isodose surface at the prescription dose level.
        /// Required for the Conformity Index; pass <c>null</c> to skip that check.
        /// </param>
        public static List<CheckResult> RunChecks(
            StructureDvhData? ptvData,
            double prescriptionDoseGy,
            double? volumeAtRxDoseCC = null)
        {
            var results = new List<CheckResult>();

            if (ptvData == null || prescriptionDoseGy <= 0)
            {
                results.Add(new CheckResult
                {
                    Name = "ICRU 83 Analysis",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Fail,
                    Message = "PTV data or prescription dose not available."
                });
                return results;
            }

            results.Add(CheckD98(ptvData, prescriptionDoseGy));
            results.Add(CheckD2(ptvData, prescriptionDoseGy));
            results.Add(CheckD50(ptvData, prescriptionDoseGy));
            results.Add(CheckMeanDose(ptvData, prescriptionDoseGy));
            results.Add(CheckHomogeneityIndex(ptvData, prescriptionDoseGy));

            if (volumeAtRxDoseCC.HasValue)
                results.Add(CheckConformityIndex(ptvData, prescriptionDoseGy, volumeAtRxDoseCC.Value));

            return results;
        }

        // ── Individual checks ──────────────────────────────────────────────────

        /// <summary>Checks D98% (near-minimum dose) ≥ 95 % of Rx (ICRU 83).</summary>
        public static CheckResult CheckD98(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double d98 = DvhCalculator.GetDoseAtVolume(ptvData, 98.0);

            if (double.IsNaN(d98))
                return new CheckResult
                {
                    Name = "PTV D98% (Near-Minimum Dose)",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate D98% – DVH data unavailable."
                };

            double pct = (d98 / prescriptionDoseGy) * 100.0;
            bool pass = pct >= MinD98Percent;

            return new CheckResult
            {
                Name = "PTV D98% (Near-Minimum Dose)",
                Standard = "ICRU 83",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(pct, 1),
                Limit = MinD98Percent,
                Unit = "% of Rx",
                Message = pass
                    ? $"D98% = {pct:F1}% of Rx | {d98:F2} Gy (criterion: ≥ {MinD98Percent}%)"
                    : $"D98% = {pct:F1}% of Rx | {d98:F2} Gy (criterion: ≥ {MinD98Percent}% – FAIL)"
            };
        }

        /// <summary>Checks D2% (near-maximum dose) ≤ 107 % of Rx (ICRU 83).</summary>
        public static CheckResult CheckD2(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double d2 = DvhCalculator.GetDoseAtVolume(ptvData, 2.0);

            if (double.IsNaN(d2))
                return new CheckResult
                {
                    Name = "PTV D2% (Near-Maximum Dose)",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate D2% – DVH data unavailable."
                };

            double pct = (d2 / prescriptionDoseGy) * 100.0;
            bool pass = pct <= MaxD2Percent;

            return new CheckResult
            {
                Name = "PTV D2% (Near-Maximum Dose)",
                Standard = "ICRU 83",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(pct, 1),
                Limit = MaxD2Percent,
                Unit = "% of Rx",
                Message = pass
                    ? $"D2% = {pct:F1}% of Rx | {d2:F2} Gy (criterion: ≤ {MaxD2Percent}%)"
                    : $"D2% = {pct:F1}% of Rx | {d2:F2} Gy (criterion: ≤ {MaxD2Percent}% – FAIL)"
            };
        }

        /// <summary>Checks D50% (median dose) within 95–107 % of Rx (ICRU 83).</summary>
        public static CheckResult CheckD50(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double d50 = DvhCalculator.GetDoseAtVolume(ptvData, 50.0);

            if (double.IsNaN(d50))
                return new CheckResult
                {
                    Name = "PTV D50% (Median Dose)",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate D50% – DVH data unavailable."
                };

            double pct = (d50 / prescriptionDoseGy) * 100.0;
            bool pass = pct >= MinD50Percent && pct <= MaxD50Percent;
            CheckStatus status = pass ? CheckStatus.Pass : (pct >= 90.0 ? CheckStatus.Warning : CheckStatus.Fail);

            return new CheckResult
            {
                Name = "PTV D50% (Median Dose)",
                Standard = "ICRU 83",
                Status = status,
                ActualValue = Math.Round(pct, 1),
                Unit = "% of Rx",
                Message = pass
                    ? $"D50% = {pct:F1}% of Rx | {d50:F2} Gy (criterion: {MinD50Percent}–{MaxD50Percent}%)"
                    : $"D50% = {pct:F1}% of Rx | {d50:F2} Gy (outside {MinD50Percent}–{MaxD50Percent}% – review)"
            };
        }

        /// <summary>Reports the PTV mean dose as a percentage of Rx (informational).</summary>
        public static CheckResult CheckMeanDose(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double mean = ptvData.MeanDoseGy;
            double pct = (mean / prescriptionDoseGy) * 100.0;
            bool pass = pct >= 95.0 && pct <= 107.0;

            return new CheckResult
            {
                Name = "PTV Mean Dose",
                Standard = "ICRU 83",
                Status = pass ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(pct, 1),
                Unit = "% of Rx",
                Message = $"Mean dose = {pct:F1}% of Rx | {mean:F2} Gy"
            };
        }

        /// <summary>
        /// Calculates the Homogeneity Index HI = (D2% − D98%) / D50%.
        /// <para>HI &lt; 0.10 – excellent; &lt; 0.20 – acceptable; ≥ 0.20 – poor.</para>
        /// </summary>
        public static CheckResult CheckHomogeneityIndex(StructureDvhData ptvData, double prescriptionDoseGy)
        {
            double d2  = DvhCalculator.GetDoseAtVolume(ptvData, 2.0);
            double d98 = DvhCalculator.GetDoseAtVolume(ptvData, 98.0);
            double d50 = DvhCalculator.GetDoseAtVolume(ptvData, 50.0);

            if (double.IsNaN(d2) || double.IsNaN(d98) || double.IsNaN(d50) || d50 <= 0)
                return new CheckResult
                {
                    Name = "Homogeneity Index (HI)",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate HI – DVH data insufficient."
                };

            double hi = (d2 - d98) / d50;
            CheckStatus status;
            string assessment;

            if (hi <= MaxHIExcellent)
            {
                status = CheckStatus.Pass;
                assessment = "Excellent dose homogeneity";
            }
            else if (hi <= MaxHIAcceptable)
            {
                status = CheckStatus.Warning;
                assessment = "Acceptable dose homogeneity";
            }
            else
            {
                status = CheckStatus.Fail;
                assessment = "Poor dose homogeneity – review distribution";
            }

            return new CheckResult
            {
                Name = "Homogeneity Index (HI)",
                Standard = "ICRU 83",
                Status = status,
                ActualValue = Math.Round(hi, 3),
                Limit = MaxHIAcceptable,
                Unit = "(D2%−D98%)/D50%",
                Message = $"HI = {hi:F3}  [D2%={d2:F2} Gy, D98%={d98:F2} Gy, D50%={d50:F2} Gy] – {assessment}"
            };
        }

        /// <summary>
        /// Calculates the Conformity Index CI = V_Rx / V_PTV.
        /// <para>CI ≈ 1 is ideal; 0.9–1.1 is good; 0.8–1.2 is acceptable.</para>
        /// </summary>
        /// <param name="ptvData">DVH data for the PTV (must have a valid <c>VolumeCC</c>).</param>
        /// <param name="prescriptionDoseGy">Prescription dose in Gy.</param>
        /// <param name="volumeAtRxDoseCC">Volume (cc) of the isodose surface at the prescription dose level.</param>
        public static CheckResult CheckConformityIndex(
            StructureDvhData ptvData,
            double prescriptionDoseGy,
            double volumeAtRxDoseCC)
        {
            if (ptvData.VolumeCC <= 0)
                return new CheckResult
                {
                    Name = "Conformity Index (CI)",
                    Standard = "ICRU 83",
                    Status = CheckStatus.Warning,
                    Message = "Could not calculate CI – PTV volume not available."
                };

            double ci = volumeAtRxDoseCC / ptvData.VolumeCC;
            CheckStatus status;
            string assessment;

            if (ci >= MinCIGood && ci <= MaxCIGood)
            {
                status = CheckStatus.Pass;
                assessment = "Excellent conformity";
            }
            else if (ci >= 0.80 && ci <= 1.20)
            {
                status = CheckStatus.Warning;
                assessment = "Acceptable conformity";
            }
            else
            {
                status = CheckStatus.Fail;
                assessment = ci < 0.80
                    ? "Under-dosing of PTV – increase coverage"
                    : "Excessive dose outside PTV – review hot spot";
            }

            return new CheckResult
            {
                Name = "Conformity Index (CI)",
                Standard = "ICRU 83",
                Status = status,
                ActualValue = Math.Round(ci, 3),
                Unit = "V_Rx / V_PTV",
                Message = $"CI = {ci:F3}  [V_Rx={volumeAtRxDoseCC:F1} cc, V_PTV={ptvData.VolumeCC:F1} cc] – {assessment}"
            };
        }
    }
}
