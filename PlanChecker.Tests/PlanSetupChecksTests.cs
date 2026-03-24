using NUnit.Framework;
using PlanChecker.Logic.Checks;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class PlanSetupChecksTests
    {
        // ── CheckPlanId ────────────────────────────────────────────────────────

        [Test]
        public void CheckPlanId_WithValidId_ReturnsPass()
        {
            var result = PlanSetupChecks.CheckPlanId("IMRT_Head");
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckPlanId_WithEmptyId_ReturnsWarning()
        {
            var result = PlanSetupChecks.CheckPlanId(string.Empty);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        [Test]
        public void CheckPlanId_WithNullId_ReturnsWarning()
        {
            var result = PlanSetupChecks.CheckPlanId(null!);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── CheckPrescription ──────────────────────────────────────────────────

        [Test]
        public void CheckPrescription_ConsistentValues_ReturnsPass()
        {
            // 60 Gy = 30 × 2 Gy
            var result = PlanSetupChecks.CheckPrescription(60.0, 2.0, 30);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckPrescription_ZeroTotalDose_ReturnsFail()
        {
            var result = PlanSetupChecks.CheckPrescription(0, 2.0, 30);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckPrescription_ZeroDosePerFraction_ReturnsFail()
        {
            var result = PlanSetupChecks.CheckPrescription(60.0, 0, 30);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckPrescription_ZeroFractions_ReturnsFail()
        {
            var result = PlanSetupChecks.CheckPrescription(60.0, 2.0, 0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckPrescription_Inconsistent_ReturnsWarning()
        {
            // 60 Gy ≠ 25 × 2 Gy = 50 Gy → Warning
            var result = PlanSetupChecks.CheckPrescription(60.0, 2.0, 25);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── CheckDoseCalculated ────────────────────────────────────────────────

        [Test]
        public void CheckDoseCalculated_WhenTrue_ReturnsPass()
        {
            var result = PlanSetupChecks.CheckDoseCalculated(true);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckDoseCalculated_WhenFalse_ReturnsFail()
        {
            var result = PlanSetupChecks.CheckDoseCalculated(false);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── CheckCalculationAlgorithm ──────────────────────────────────────────

        [Test]
        public void CheckCalculationAlgorithm_ValidName_ReturnsPass()
        {
            var result = PlanSetupChecks.CheckCalculationAlgorithm("AAA_15.0.1");
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckCalculationAlgorithm_Empty_ReturnsWarning()
        {
            var result = PlanSetupChecks.CheckCalculationAlgorithm(string.Empty);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── CheckDoseMatrixResolution ──────────────────────────────────────────

        [Test]
        public void CheckDoseMatrixResolution_2mm_ReturnsPass()
        {
            var result = PlanSetupChecks.CheckDoseMatrixResolution(2.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckDoseMatrixResolution_3mm_ReturnsPass()
        {
            var result = PlanSetupChecks.CheckDoseMatrixResolution(3.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckDoseMatrixResolution_4mm_ReturnsWarning()
        {
            var result = PlanSetupChecks.CheckDoseMatrixResolution(4.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        [Test]
        public void CheckDoseMatrixResolution_6mm_ReturnsFail()
        {
            var result = PlanSetupChecks.CheckDoseMatrixResolution(6.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── RunChecks (integration) ────────────────────────────────────────────

        [Test]
        public void RunChecks_FullyConfiguredPlan_ReturnsSixResults()
        {
            var results = PlanSetupChecks.RunChecks(
                planId                  : "IMRT_001",
                prescriptionDoseGy      : 60.0,
                dosePerFractionGy       : 2.0,
                numberOfFractions       : 30,
                isPlanCalculated        : true,
                calculationAlgorithm    : "AAA_15",
                normalizationMethod     : "100% covers 95% of PTV",
                doseMatrixResolutionMm  : 2.5);

            Assert.That(results.Count, Is.EqualTo(6));
            Assert.That(results.TrueForAll(r => r.Status == CheckStatus.Pass));
        }

        [Test]
        public void RunChecks_WithoutGridResolution_ReturnsFiveResults()
        {
            var results = PlanSetupChecks.RunChecks(
                planId               : "Plan1",
                prescriptionDoseGy   : 50.0,
                dosePerFractionGy    : 2.0,
                numberOfFractions    : 25,
                isPlanCalculated     : true,
                calculationAlgorithm : "AXB_15",
                normalizationMethod  : string.Empty,
                doseMatrixResolutionMm: 0);   // skip resolution check

            Assert.That(results.Count, Is.EqualTo(5));
        }
    }
}
