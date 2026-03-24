using NUnit.Framework;
using PlanChecker.Logic.Checks;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class OARChecksTests
    {
        // ── Spinal cord ───────────────────────────────────────────────────────

        [Test]
        public void CheckSpinalCord_Below45Gy_ReturnsPass()
        {
            var cord = DvhFactory.OAR("SpinalCord", maxDoseGy: 44.0);
            var result = OARChecks.CheckSpinalCord(cord);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckSpinalCord_Between45And50_ReturnsWarning()
        {
            var cord = new StructureDvhData
            {
                StructureId = "Cord", MaxDoseGy = 47.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckSpinalCord(cord);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        [Test]
        public void CheckSpinalCord_Above50Gy_ReturnsFail()
        {
            var cord = new StructureDvhData
            {
                StructureId = "Cord", MaxDoseGy = 52.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckSpinalCord(cord);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── Brainstem ─────────────────────────────────────────────────────────

        [Test]
        public void CheckBrainstem_Below54Gy_ReturnsPass()
        {
            var bs = new StructureDvhData
            {
                StructureId = "Brainstem", MaxDoseGy = 50.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckBrainstem(bs);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckBrainstem_Above54Gy_ReturnsFail()
        {
            var bs = new StructureDvhData
            {
                StructureId = "Brainstem", MaxDoseGy = 56.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckBrainstem(bs);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
        }

        // ── Lens ──────────────────────────────────────────────────────────────

        [Test]
        public void CheckLens_Below5Gy_ReturnsPass()
        {
            var lens = new StructureDvhData
            {
                StructureId = "Lens_L", MaxDoseGy = 3.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckLens(lens);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
        }

        [Test]
        public void CheckLens_Above5Gy_ReturnsWarning()
        {
            var lens = new StructureDvhData
            {
                StructureId = "Lens_L", MaxDoseGy = 7.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var result = OARChecks.CheckLens(lens);
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warning));
        }

        // ── Lung ──────────────────────────────────────────────────────────────

        [Test]
        public void CheckLung_LowDose_BothChecksPass()
        {
            // V20Gy = 10%, mean = 6 Gy → both pass
            var lung = DvhFactory.OAR("Lung_L", maxDoseGy: 30.0, volumeCC: 1500.0);
            var results = OARChecks.CheckLung(lung);
            Assert.That(results.TrueForAll(r => r.Status == CheckStatus.Pass));
        }

        [Test]
        public void CheckLung_V20HighDose_V20Fails()
        {
            // Uniform 25 Gy → V20Gy = 100% → Fail
            var lung = DvhFactory.Uniform("Lung_R", 25.0, volumeCC: 1200.0);
            var results = OARChecks.CheckLung(lung);
            Assert.That(results.Exists(r => r.Name.Contains("V20") && r.Status == CheckStatus.Fail));
        }

        // ── Rectum ────────────────────────────────────────────────────────────

        [Test]
        public void CheckRectum_LowDose_AllConstraintsPass()
        {
            // Max 40 Gy → all dose-volume constraints satisfied
            var rectum = DvhFactory.OAR("Rectum", maxDoseGy: 40.0, volumeCC: 80.0);
            var results = OARChecks.CheckRectum(rectum);
            Assert.That(results.TrueForAll(r => r.Status == CheckStatus.Pass));
        }

        [Test]
        public void CheckRectum_HighDose_SomeConstraintsFail()
        {
            // Uniform 72 Gy → V70Gy = 100% → Fail (criterion ≤ 20%)
            var rectum = DvhFactory.Uniform("Rectum", 72.0, volumeCC: 80.0);
            var results = OARChecks.CheckRectum(rectum);
            Assert.That(results.Exists(r => r.Status == CheckStatus.Fail));
        }

        // ── RunChecks structure matching ──────────────────────────────────────

        [Test]
        public void RunChecks_SpinalCordByName_IsEvaluated()
        {
            var cord = new StructureDvhData
            {
                StructureId = "SpinalCord", MaxDoseGy = 40.0,
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var results = OARChecks.RunChecks(new[] { cord });
            Assert.That(results.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RunChecks_UnknownStructure_IsSkipped()
        {
            var unknown = new StructureDvhData
            {
                StructureId = "ZMarker",
                CumulativeDvh = System.Array.Empty<DvhDataPoint>(),
            };
            var results = OARChecks.RunChecks(new[] { unknown });
            Assert.That(results.Count, Is.EqualTo(0));
        }
    }
}
