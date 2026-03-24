using System;
using System.Collections.Generic;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Checks
{
    /// <summary>
    /// General plan-setup checks that are prerequisites for ICRU 62/83 compliance.
    /// </summary>
    public static class PlanSetupChecks
    {
        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Runs all plan-setup checks and returns the list of results.
        /// </summary>
        /// <param name="planId">Eclipse plan identifier.</param>
        /// <param name="prescriptionDoseGy">Total prescription dose in Gy.</param>
        /// <param name="dosePerFractionGy">Dose per fraction in Gy.</param>
        /// <param name="numberOfFractions">Number of fractions.</param>
        /// <param name="isPlanCalculated">Whether the plan has been dose-calculated.</param>
        /// <param name="calculationAlgorithm">Name of the dose-calculation algorithm.</param>
        /// <param name="normalizationMethod">Plan normalization description (optional).</param>
        /// <param name="doseMatrixResolutionMm">Dose-matrix voxel size in mm (pass ≤ 0 to skip).</param>
        public static List<CheckResult> RunChecks(
            string planId,
            double prescriptionDoseGy,
            double dosePerFractionGy,
            int numberOfFractions,
            bool isPlanCalculated,
            string calculationAlgorithm,
            string normalizationMethod,
            double doseMatrixResolutionMm = 0.0)
        {
            var results = new List<CheckResult>();

            results.Add(CheckPlanId(planId));
            results.Add(CheckPrescription(prescriptionDoseGy, dosePerFractionGy, numberOfFractions));
            results.Add(CheckDoseCalculated(isPlanCalculated));
            results.Add(CheckCalculationAlgorithm(calculationAlgorithm));
            results.Add(CheckNormalizationMethod(normalizationMethod));

            if (doseMatrixResolutionMm > 0)
                results.Add(CheckDoseMatrixResolution(doseMatrixResolutionMm));

            return results;
        }

        // ── Individual checks ──────────────────────────────────────────────────

        /// <summary>Verifies that the plan has a non-empty identifier.</summary>
        public static CheckResult CheckPlanId(string planId)
        {
            bool valid = !string.IsNullOrWhiteSpace(planId);
            return new CheckResult
            {
                Name = "Plan Identifier",
                Standard = "Plan Setup",
                Status = valid ? CheckStatus.Pass : CheckStatus.Warning,
                Message = valid ? $"Plan ID: {planId}" : "Plan ID is not set."
            };
        }

        /// <summary>
        /// Verifies the prescription: total dose, dose-per-fraction and number of
        /// fractions are all set and arithmetically consistent.
        /// </summary>
        public static CheckResult CheckPrescription(
            double totalDoseGy,
            double dosePerFractionGy,
            int numberOfFractions)
        {
            if (totalDoseGy <= 0)
                return new CheckResult
                {
                    Name = "Prescription",
                    Standard = "Plan Setup",
                    Status = CheckStatus.Fail,
                    Message = "Total prescription dose not set."
                };

            if (dosePerFractionGy <= 0)
                return new CheckResult
                {
                    Name = "Prescription",
                    Standard = "Plan Setup",
                    Status = CheckStatus.Fail,
                    Message = "Dose per fraction not set."
                };

            if (numberOfFractions <= 0)
                return new CheckResult
                {
                    Name = "Prescription",
                    Standard = "Plan Setup",
                    Status = CheckStatus.Fail,
                    Message = "Number of fractions not set."
                };

            double calculated = dosePerFractionGy * numberOfFractions;
            bool consistent = Math.Abs(calculated - totalDoseGy) <= 0.01;   // 10 mGy tolerance

            return new CheckResult
            {
                Name = "Prescription",
                Standard = "Plan Setup",
                Status = consistent ? CheckStatus.Pass : CheckStatus.Warning,
                Message = consistent
                    ? $"{totalDoseGy:F2} Gy in {numberOfFractions} × {dosePerFractionGy:F2} Gy fractions"
                    : $"Prescription inconsistency: {totalDoseGy:F2} Gy ≠ " +
                      $"{numberOfFractions} × {dosePerFractionGy:F2} Gy = {calculated:F2} Gy"
            };
        }

        /// <summary>Verifies that the plan dose has been calculated.</summary>
        public static CheckResult CheckDoseCalculated(bool isPlanCalculated)
        {
            return new CheckResult
            {
                Name = "Dose Calculation",
                Standard = "Plan Setup",
                Status = isPlanCalculated ? CheckStatus.Pass : CheckStatus.Fail,
                Message = isPlanCalculated
                    ? "Plan dose has been calculated."
                    : "Plan dose has NOT been calculated."
            };
        }

        /// <summary>Verifies that a dose-calculation algorithm is specified.</summary>
        public static CheckResult CheckCalculationAlgorithm(string algorithm)
        {
            bool valid = !string.IsNullOrWhiteSpace(algorithm);
            return new CheckResult
            {
                Name = "Calculation Algorithm",
                Standard = "Plan Setup",
                Status = valid ? CheckStatus.Pass : CheckStatus.Warning,
                Message = valid
                    ? $"Algorithm: {algorithm}"
                    : "Calculation algorithm not specified."
            };
        }

        /// <summary>Reports the plan normalization method (informational).</summary>
        public static CheckResult CheckNormalizationMethod(string normalization)
        {
            bool valid = !string.IsNullOrWhiteSpace(normalization);
            return new CheckResult
            {
                Name = "Plan Normalization",
                Standard = "Plan Setup",
                Status = valid ? CheckStatus.Pass : CheckStatus.Info,
                Message = valid
                    ? $"Normalization: {normalization}"
                    : "Normalization method not specified."
            };
        }

        /// <summary>
        /// Checks the dose-matrix resolution.
        /// ICRU 83 recommends a grid spacing ≤ 3 mm for IMRT.
        /// A spacing &gt; 5 mm is flagged as a hard failure.
        /// </summary>
        public static CheckResult CheckDoseMatrixResolution(double resolutionMm)
        {
            bool good       = resolutionMm <= 3.0;
            bool acceptable = resolutionMm <= 5.0;

            return new CheckResult
            {
                Name = "Dose Matrix Resolution",
                Standard = "ICRU 83",
                Status = good ? CheckStatus.Pass : (acceptable ? CheckStatus.Warning : CheckStatus.Fail),
                ActualValue = resolutionMm,
                Limit = 3.0,
                Unit = "mm",
                Message = good
                    ? $"Dose-grid resolution: {resolutionMm:F1} mm (≤ 3 mm – recommended for IMRT)"
                    : acceptable
                        ? $"Dose-grid resolution: {resolutionMm:F1} mm (> 3 mm – may reduce IMRT accuracy)"
                        : $"Dose-grid resolution: {resolutionMm:F1} mm (> 5 mm – poor calculation accuracy)"
            };
        }
    }
}
