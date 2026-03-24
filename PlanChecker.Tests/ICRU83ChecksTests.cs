using NUnit.Framework;
using PlanChecker.Logic.Checks;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class ICRU83ChecksTests
    {
        private const double RxGy = 60.0;

        // ── CheckD98 ──────────────────────────────────────────────────────────

        [Test]
        public void CheckD98_WhenUniformAtRx_ReturnsPass()
        {
            var ptv = DvhFactory.Uniform("PTV", RxGy);
            var result = ICRU83Checks.CheckD98(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckD98_WhenD98Below95Pct_ReturnsFail()
        {
            // Linear from 54 Gy (90% of 60) to 66 Gy
            // D98% ≈ 54.2 Gy  → 90.3% of Rx → Fail
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 54.0, maxDoseGy: 66.0);
            var result = ICRU83Checks.CheckD98(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckD98_WhenD98Is97Pct_ReturnsPass()
        {
            // Linear from 58 Gy (96.7%) to 62 Gy
            // D98% ≈ 58.1 Gy  → 96.8% of Rx → Pass
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 58.0, maxDoseGy: 62.0);
            var result = ICRU83Checks.CheckD98(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        // ── CheckD2 ───────────────────────────────────────────────────────────

        [Test]
        public void CheckD2_WhenUniformAtRx_ReturnsPass()
        {
            var ptv = DvhFactory.Uniform("PTV", RxGy);
            var result = ICRU83Checks.CheckD2(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckD2_WhenD2Above107Pct_ReturnsFail()
        {
            // Linear from 60 Gy to 67 Gy (111.7%)
            // D2% ≈ 66.9 Gy → 111.4% of Rx → Fail
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 60.0, maxDoseGy: 67.0);
            var result = ICRU83Checks.CheckD2(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckD2_WhenD2Is105Pct_ReturnsPass()
        {
            // Linear from 57 Gy to 63 Gy (105%)
            // D2% ≈ 62.9 Gy → 104.8% of Rx → Pass
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 57.0, maxDoseGy: 63.0);
            var result = ICRU83Checks.CheckD2(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        // ── CheckD50 ──────────────────────────────────────────────────────────

        [Test]
        public void CheckD50_WhenMedianEqualsRx_ReturnsPass()
        {
            // Symmetric linear DVH centred on Rx → D50% = Rx
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 57.0, maxDoseGy: 63.0);
            var result = ICRU83Checks.CheckD50(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckD50_WhenMedianOutOfRange_ReturnsNotPass()
        {
            // Shift the whole curve down: min 48 Gy, max 54 Gy → D50% = 51 Gy (85% of Rx)
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 48.0, maxDoseGy: 54.0);
            var result = ICRU83Checks.CheckD50(ptv, RxGy);
            Assert.That(result.Status, Is.Not.EqualTo(CheckStatus.Pass));
        }

        // ── CheckHomogeneityIndex ─────────────────────────────────────────────

        [Test]
        public void CheckHomogeneityIndex_UniformDvh_ReturnsPassWithHINearZero()
        {
            var ptv = DvhFactory.Uniform("PTV", RxGy);
            var result = ICRU83Checks.CheckHomogeneityIndex(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            // HI should be ≈ 0 for a uniform distribution
            Assert.That(result.ActualValue, Is.LessThanOrEqualTo(ICRU83Checks.MaxHIExcellent));
        }

        [Test]
        public void CheckHomogeneityIndex_NarrowSpread_ReturnsExcellent()
        {
            // D2%=61.8 Gy, D98%=58.2 Gy, D50%=60 Gy → HI=(61.8-58.2)/60=0.06 → Pass
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 58.0, maxDoseGy: 62.0);
            var result = ICRU83Checks.CheckHomogeneityIndex(ptv, RxGy);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckHomogeneityIndex_WideSpread_ReturnsPoor()
        {
            // D2%≈65 Gy, D98%≈55 Gy, D50%=60 Gy → HI=(65-55)/60≈0.167 → Warning or Fail
            var ptv = DvhFactory.Linear("PTV", minDoseGy: 54.0, maxDoseGy: 66.0);
            var result = ICRU83Checks.CheckHomogeneityIndex(ptv, RxGy);
            Assert.That(result.Status, Is.AnyOf(CheckStatus.Warning, CheckStatus.Fail));
        }

        // ── CheckConformityIndex ──────────────────────────────────────────────

        [Test]
        public void CheckConformityIndex_WhenVrxEqualsVptv_ReturnsPass()
        {
            var ptv = new StructureDvhData
            {
                StructureId = "PTV", VolumeCC = 150.0,
                MinDoseGy = 58.0, MaxDoseGy = 62.0, MeanDoseGy = 60.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            // V_Rx = V_PTV → CI = 1.0 → Pass
            var result = ICRU83Checks.CheckConformityIndex(ptv, RxGy, volumeAtRxDoseCC: 150.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.ActualValue, Is.EqualTo(1.0).Within(0.001));
        }

        [Test]
        public void CheckConformityIndex_WhenVrxIsHalfVptv_ReturnsFail()
        {
            var ptv = new StructureDvhData
            {
                StructureId = "PTV", VolumeCC = 150.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            // CI = 75 / 150 = 0.5 → Fail (under-dosing)
            var result = ICRU83Checks.CheckConformityIndex(ptv, RxGy, volumeAtRxDoseCC: 75.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        [Test]
        public void CheckConformityIndex_WhenVrxIsDoubleVptv_ReturnsFail()
        {
            var ptv = new StructureDvhData
            {
                StructureId = "PTV", VolumeCC = 100.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            // CI = 200 / 100 = 2.0 → Fail (excessive dose outside PTV)
            var result = ICRU83Checks.CheckConformityIndex(ptv, RxGy, volumeAtRxDoseCC: 200.0);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── RunChecks (integration) ────────────────────────────────────────────

        [Test]
        public void RunChecks_GoodImtPlan_AllChecksShouldReturnResult()
        {
            // A tight uniform plan around Rx produces results for all checks
            var ptv = DvhFactory.Uniform("PTV", RxGy);
            var results = ICRU83Checks.RunChecks(ptv, RxGy, volumeAtRxDoseCC: 100.0);

            Assert.That(results.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void RunChecks_NullPtvData_ReturnsSingleFailResult()
        {
            var results = ICRU83Checks.RunChecks(null, RxGy);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Status, Is.EqualTo(CheckStatus.Fail));
        }
    }
}
