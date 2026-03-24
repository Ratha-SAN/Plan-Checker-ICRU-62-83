// ============================================================================
//  PlanChecker – Varian Eclipse ESAPI Entry Point
//  Checks treatment plans for compliance with ICRU Reports 62 and 83.
//
//  How to deploy
//  -------------
//  1. Copy the compiled PlanChecker.dll (and PlanChecker.Logic.dll) into
//     your Eclipse scripting folder.
//  2. Open any calculated plan in Eclipse and run the script via
//     "Tools > Scripts..." (or the Scripting node in the administration tool).
//
//  The script generates a self-contained HTML report and opens it in the
//  default browser.  A summary dialog is also shown inside Eclipse.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Windows;

using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

using PlanChecker.Logic.Checks;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;

// Required by Varian to mark the script as non-writeable (read-only access).
[assembly: ESAPIScript(IsWriteable = false)]

namespace VMS.TPS
{
    /// <summary>
    /// Eclipse scripting entry point.  Eclipse calls <see cref="Execute"/> once
    /// when the script is invoked from the Scripting menu.
    /// </summary>
    public class Script
    {
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public void Execute(ScriptContext context)
        {
            if (context?.PlanSetup == null)
            {
                MessageBox.Show(
                    "No plan is currently open.\n\n" +
                    "Please open a calculated plan and run the script again.",
                    "Plan Checker – ICRU 62/83",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                PlanCheckReport report = BuildReport(context);

                string html     = ReportGenerator.GenerateHtmlReport(report);
                string tempFile = Path.Combine(
                    Path.GetTempPath(),
                    $"PlanChecker_{Sanitise(report.PatientId)}_{Sanitise(report.PlanId)}" +
                    $"_{DateTime.Now:yyyyMMdd_HHmmss}.html");

                File.WriteAllText(tempFile, html, System.Text.Encoding.UTF8);

                // Open in the default browser
                System.Diagnostics.Process.Start(tempFile);

                // Brief summary inside Eclipse
                int fails = CountStatus(report, CheckStatus.Fail);
                int warns = CountStatus(report, CheckStatus.Warning);
                int pass  = CountStatus(report, CheckStatus.Pass);

                string summary =
                    $"Patient : {report.PatientName}  ({report.PatientId})\n" +
                    $"Plan    : {report.PlanId}  (Course: {report.CourseId})\n" +
                    $"Rx      : {report.PrescriptionDoseGy:F2} Gy in {report.NumberOfFractions} fx\n\n" +
                    $"Results : {pass} Pass   {warns} Warning   {fails} Fail\n\n" +
                    $"Full report saved to:\n{tempFile}";

                MessageBox.Show(
                    summary,
                    "Plan Checker – ICRU 62/83",
                    MessageBoxButton.OK,
                    fails > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while running the Plan Checker:\n\n{ex.Message}",
                    "Plan Checker – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ── Report builder ─────────────────────────────────────────────────────

        private static PlanCheckReport BuildReport(ScriptContext context)
        {
            PlanSetup plan = context.PlanSetup;
            Course    course = context.Course;

            // ── Prescription ─────────────────────────────────────────────────
            double rxGy    = ToGy(plan.TotalDose);
            double fxGy    = ToGy(plan.UniqueFractionation?.PrescribedDosePerFraction ?? default);
            int    nFx     = plan.UniqueFractionation?.NumberOfFractions ?? 0;

            // ── Technique / algorithm ─────────────────────────────────────────
            string technique  = plan.Beams.FirstOrDefault()?.Technique?.ToString() ?? "Unknown";
            string algorithm  = plan.GetCalculationModel(CalculationType.PhotonVolumeDose) ?? "Unknown";
            string normalise  = plan.PlanNormalizationValue.ToString("F1") + "%";

            // ── Build report shell ────────────────────────────────────────────
            var report = new PlanCheckReport
            {
                PatientId           = context.Patient.Id,
                PatientName         = $"{context.Patient.LastName}, {context.Patient.FirstName}",
                PlanId              = plan.Id,
                CourseId            = course?.Id ?? string.Empty,
                PrescriptionDoseGy  = rxGy,
                DosePerFractionGy   = fxGy,
                NumberOfFractions   = nFx,
                PlanningTechnique   = technique,
                CalculationAlgorithm = algorithm,
                GeneratedAt         = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            bool isPlanCalculated = plan.Dose != null;

            // ── Plan-setup checks ─────────────────────────────────────────────
            double gridMm = plan.Dose?.XRes ?? 0.0;   // dose-grid voxel size in mm
            report.PlanSetupResults = PlanSetupChecks.RunChecks(
                planId               : plan.Id,
                prescriptionDoseGy   : rxGy,
                dosePerFractionGy    : fxGy,
                numberOfFractions    : nFx,
                isPlanCalculated     : isPlanCalculated,
                calculationAlgorithm : algorithm,
                normalizationMethod  : normalise,
                doseMatrixResolutionMm: gridMm);

            if (!isPlanCalculated || rxGy <= 0)
                return report;

            // ── Identify target structures ────────────────────────────────────
            StructureSet ss = plan.StructureSet;
            Structure? ptv = FindStructure(ss, "PTV");
            Structure? ctv = FindStructure(ss, "CTV");
            Structure? gtv = FindStructure(ss, "GTV");

            StructureDvhData? ptvDvh = ptv != null ? ExtractDvh(plan, ptv) : null;

            bool hasPTV = ptvDvh != null;
            bool hasCTV = ctv != null;
            bool hasGTV = gtv != null;

            // ── Global hot-spot (Dmax over entire dose grid) ──────────────────
            double globalMaxGy = ToGy(plan.Dose?.DoseMax3D ?? default);

            // ── Volume covered by prescription dose (for CI) ──────────────────
            double? vRxCC = null;
            if (ptv != null && rxGy > 0)
            {
                // V(Rx Gy) across the whole body structure (or Body)
                Structure? body = ss.Structures
                    .FirstOrDefault(s =>
                        s.Id.Equals("BODY", StringComparison.OrdinalIgnoreCase) ||
                        s.Id.Equals("EXTERNAL", StringComparison.OrdinalIgnoreCase) ||
                        s.DicomType.Equals("EXTERNAL", StringComparison.OrdinalIgnoreCase));

                if (body != null)
                {
                    DoseValue rxDv = new DoseValue(rxGy * 100, DoseValue.DoseUnit.cGy);
                    vRxCC = plan.GetVolumeAtDose(body, rxDv, VolumePresentation.AbsoluteCm3);
                }
            }

            // ── ICRU 62 checks ────────────────────────────────────────────────
            report.ICRU62Results = ICRU62Checks.RunChecks(
                ptvData            : ptvDvh,
                prescriptionDoseGy : rxGy,
                hasPTV             : hasPTV,
                hasCTV             : hasCTV,
                hasGTV             : hasGTV,
                globalMaxDoseGy    : globalMaxGy);

            // ── ICRU 83 checks ────────────────────────────────────────────────
            report.ICRU83Results = ICRU83Checks.RunChecks(
                ptvData            : ptvDvh,
                prescriptionDoseGy : rxGy,
                volumeAtRxDoseCC   : vRxCC);

            // ── OAR checks ────────────────────────────────────────────────────
            var oarDvhList = new List<StructureDvhData>();
            foreach (Structure s in ss.Structures)
            {
                if (s.IsEmpty) continue;
                if (s.DicomType == "PTV" || s.DicomType == "CTV" || s.DicomType == "GTV" ||
                    s.DicomType == "ITV" || s.DicomType == "EXTERNAL" ||
                    s.Id.Equals(ptv?.Id, StringComparison.OrdinalIgnoreCase) ||
                    s.Id.Equals(ctv?.Id, StringComparison.OrdinalIgnoreCase) ||
                    s.Id.Equals(gtv?.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                var dvh = ExtractDvh(plan, s);
                if (dvh != null)
                    oarDvhList.Add(dvh);
            }
            report.OARResults = OARChecks.RunChecks(oarDvhList);

            return report;
        }

        // ── DVH extraction helpers ─────────────────────────────────────────────

        /// <summary>
        /// Converts a DVHData curve from ESAPI to the <see cref="StructureDvhData"/>
        /// used by the pure-logic layer.  Dose values in ESAPI may be in cGy; this
        /// method always converts to Gy.
        /// </summary>
        private static StructureDvhData? ExtractDvh(PlanSetup plan, Structure structure)
        {
            if (structure == null || structure.IsEmpty) return null;

            try
            {
                DVHData dvhData = plan.GetDVHCumulativeData(
                    structure,
                    DoseValuePresentation.Absolute,
                    VolumePresentation.Relative,
                    0.001);   // 0.001 Gy bin width for high resolution

                if (dvhData?.CurveData == null || dvhData.CurveData.Length == 0)
                    return null;

                // Build the DvhDataPoint array; ensure dose is in Gy
                bool isCGy = dvhData.MaxDose.Unit == DoseValue.DoseUnit.cGy;
                double conv = isCGy ? 0.01 : 1.0;

                var points = new DvhDataPoint[dvhData.CurveData.Length];
                for (int i = 0; i < dvhData.CurveData.Length; i++)
                {
                    points[i] = new DvhDataPoint
                    {
                        DoseGy        = dvhData.CurveData[i].DoseValue.Dose * conv,
                        VolumePercent = dvhData.CurveData[i].Volume,
                    };
                }

                return new StructureDvhData
                {
                    StructureId  = structure.Id,
                    VolumeCC     = structure.Volume,
                    MinDoseGy    = dvhData.MinDose.Dose * conv,
                    MaxDoseGy    = dvhData.MaxDose.Dose * conv,
                    MeanDoseGy   = dvhData.MeanDose.Dose * conv,
                    CumulativeDvh = points,
                };
            }
            catch
            {
                return null;
            }
        }

        // ── Utility methods ────────────────────────────────────────────────────

        /// <summary>Converts a <see cref="DoseValue"/> to Gy regardless of its unit.</summary>
        private static double ToGy(DoseValue dv)
        {
            if (dv == default) return 0;
            return dv.Unit == DoseValue.DoseUnit.cGy ? dv.Dose / 100.0 : dv.Dose;
        }

        /// <summary>
        /// Finds the first structure whose Id contains <paramref name="keyword"/>
        /// (case-insensitive).
        /// </summary>
        private static Structure? FindStructure(StructureSet ss, string keyword)
            => ss?.Structures
                  .Where(s => !s.IsEmpty)
                  .FirstOrDefault(s => s.Id.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Counts results with the given status across all report sections.</summary>
        private static int CountStatus(PlanCheckReport report, CheckStatus status)
        {
            int count = 0;
            foreach (var list in new[] { report.PlanSetupResults, report.ICRU62Results,
                                         report.ICRU83Results,    report.OARResults })
            {
                foreach (var r in list)
                    if (r.Status == status) count++;
            }
            return count;
        }

        /// <summary>Removes characters that are unsafe in a file name.</summary>
        private static string Sanitise(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
