using NUnit.Framework;
using PlanChecker.Logic.Checks;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class ICRU62ChecksTests
    {
        private const double RxGy = 60.0;   // 60 Gy prescription for all tests

        // ── CheckTargetVolumes ─────────────────────────────────────────────────

        [Test]
        public void CheckTargetVolumes_AllPresent_ReturnsPass()
        {
            var result = ICRU62Checks.CheckTargetVolumes(hasPTV: true, hasCTV: true, hasGTV: true);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckTargetVolumes_NoPTV_ReturnsFail()
        {
            var result = ICRU62Checks.CheckTargetVolumes(hasPTV: false, hasCTV: true, hasGTV: true);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckTargetVolumes_PTVOnlyNoCTVGTV_ReturnsWarning()
        {
            var result = ICRU62Checks.CheckTargetVolumes(hasPTV: true, hasCTV: false, hasGTV: false);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── CheckPTVD95Coverage ───────────────────────────────────────────────

        [Test]
        public void CheckPTVD95Coverage_WhenD95EqualsRx_ReturnsPass()
        {
            // Uniform DVH at exactly Rx dose → D95% = Rx → 100 % → Pass
            var ptv = DvhFactory.Uniform("PTV", RxGy);
            var result = ICRU62Checks.CheckPTVD95Coverage(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckPTVD95Coverage_WhenD95Is96PctOfRx_ReturnsPass()
        {
            // Linear DVH: min = 57 Gy (95% of 60), max = 63 Gy
            // D95% ≈ 57.3 Gy  →  95.5 % of Rx  →  Pass
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 57.0, maxDoseGy: 63.0);
            var result = ICRU62Checks.CheckPTVD95Coverage(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckPTVD95Coverage_WhenD95Below95Pct_ReturnsFail()
        {
            // D95% will be around 55 Gy (91.7% of 60 Gy) → Fail
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 54.0, maxDoseGy: 66.0);
            var result = ICRU62Checks.CheckPTVD95Coverage(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── CheckPTVMaxDose ───────────────────────────────────────────────────

        [Test]
        public void CheckPTVMaxDose_WhenMaxIs105Pct_ReturnsPass()
        {
            double max = RxGy * 1.05;
            var ptv = DvhFactory.Uniform("PTV", max);
            ptv = new StructureDvhData
            {
                StructureId   = "PTV", VolumeCC = 100,
                MinDoseGy = max, MaxDoseGy = max, MeanDoseGy = max,
                CumulativeDvh = ptv.CumulativeDvh,
            };
            var result = ICRU62Checks.CheckPTVMaxDose(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckPTVMaxDose_WhenMaxIs110Pct_ReturnsFail()
        {
            double max = RxGy * 1.10;
            var ptv = new StructureDvhData
            {
                StructureId = "PTV", VolumeCC = 100,
                MinDoseGy = RxGy * 0.95, MaxDoseGy = max, MeanDoseGy = RxGy,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = ICRU62Checks.CheckPTVMaxDose(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── CheckICRUReferencePoint ────────────────────────────────────────────

        [Test]
        public void CheckICRUReferencePoint_WhenInRange_ReturnsPass()
        {
            // 100 % of Rx is within 95–107 %
            var result = ICRU62Checks.CheckICRUReferencePoint(RxGy, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckICRUReferencePoint_WhenBelow95Pct_ReturnsFail()
        {
            var result = ICRU62Checks.CheckICRUReferencePoint(RxGy * 0.94, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckICRUReferencePoint_WhenAbove107Pct_ReturnsFail()
        {
            var result = ICRU62Checks.CheckICRUReferencePoint(RxGy * 1.08, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── CheckGlobalHotSpot ─────────────────────────────────────────────────

        [Test]
        public void CheckGlobalHotSpot_When108PctOfRx_ReturnsPass()
        {
            var result = ICRU62Checks.CheckGlobalHotSpot(RxGy * 1.08, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckGlobalHotSpot_When115PctOfRx_ReturnsWarning()
        {
            var result = ICRU62Checks.CheckGlobalHotSpot(RxGy * 1.15, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── RunChecks (integration) ────────────────────────────────────────────

        [Test]
        public void RunChecks_GoodPlan_AllCriticalChecksShouldPass()
        {
            // A near-perfect uniform plan at exactly Rx dose
            var ptv = DvhFactory.Uniform("PTV", RxGy);

            var results = ICRU62Checks.RunChecks(
                ptvData: ptv,
                prescriptionDoseGy: RxGy,
                hasPTV: true,
                hasCTV: true,
                hasGTV: true,
                globalMaxDoseGy: RxGy * 1.03);

            // The D95 and Dmax checks must pass for a uniform-at-Rx plan
            Assert.That(results.Exists(r => r.Name.Contains("D95") && r.Status == CheckStatus.Pass));
            Assert.That(results.Exists(r => r.Name.Contains("Maximum") && r.Status == CheckStatus.Pass));
        }

        [Test]
        public void RunChecks_NullPtvData_ReturnsOnlyStructureCheck()
        {
            var results = ICRU62Checks.RunChecks(
                ptvData: null,
                prescriptionDoseGy: RxGy,
                hasPTV: true, hasCTV: false, hasGTV: false);

            // With null PTV data only the structure-existence check is returned
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Name, Does.Contain("Target Volume"));
        }
    }
}
