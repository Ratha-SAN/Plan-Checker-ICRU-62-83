using System.Collections.Generic;

namespace PlanChecker.Logic.Models
{
    /// <summary>
    /// Container for all plan-check results that will be rendered into the HTML report.
    /// </summary>
    public class PlanCheckReport
    {
        // ── Patient / plan identification ──────────────────────────────────────
        public string PatientId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PlanId { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;

        // ── Prescription ───────────────────────────────────────────────────────
        public double PrescriptionDoseGy { get; set; }
        public double DosePerFractionGy { get; set; }
        public int NumberOfFractions { get; set; }

        // ── Plan metadata ──────────────────────────────────────────────────────
        public string PlanningTechnique { get; set; } = string.Empty;
        public string CalculationAlgorithm { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;

        // ── Check-result collections ───────────────────────────────────────────
        public List<CheckResult> PlanSetupResults { get; set; } = new List<CheckResult>();
        public List<CheckResult> ICRU62Results { get; set; } = new List<CheckResult>();
        public List<CheckResult> ICRU83Results { get; set; } = new List<CheckResult>();
        public List<CheckResult> OARResults { get; set; } = new List<CheckResult>();
    }
}
