using NUnit.Framework;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class DvhCalculatorTests
    {
        // ── GetDoseAtVolume ────────────────────────────────────────────────────

        [Test]
        public void GetDoseAtVolume_UniformDvh_ReturnsSameDoseAtAnyVolume()
        {
            var dvh = DvhFactory.Uniform("PTV", uniformDoseGy: 60.0);

            Assert.That(DvhCalculator.GetDoseAtVolume(dvh, 2.0),  Is.EqualTo(60.0).Within(0.01));
            Assert.That(DvhCalculator.GetDoseAtVolume(dvh, 50.0), Is.EqualTo(60.0).Within(0.01));
            Assert.That(DvhCalculator.GetDoseAtVolume(dvh, 98.0), Is.EqualTo(60.0).Within(0.01));
        }

        [Test]
        public void GetDoseAtVolume_LinearDvh_InterpolatesCorrectly()
        {
            // DVH ramps linearly: D0% = 66 Gy, D100% = 54 Gy  →  D50% = 60 Gy
            var dvh = DvhFactory.Linear("PTV", minDoseGy: 54.0, maxDoseGy: 66.0);

            double d50 = DvhCalculator.GetDoseAtVolume(dvh, 50.0);
            Assert.That(d50, Is.EqualTo(60.0).Within(0.1));
        }

        [Test]
        public void GetDoseAtVolume_LinearDvh_D98_CloseToMinDose()
        {
            var dvh = DvhFactory.Linear("PTV", minDoseGy: 55.0, maxDoseGy: 65.0);

            // D98% should be very close to the minimum dose (55 Gy)
            double d98 = DvhCalculator.GetDoseAtVolume(dvh, 98.0);
            Assert.That(d98, Is.EqualTo(55.2).Within(0.3));
        }

        [Test]
        public void GetDoseAtVolume_LinearDvh_D2_CloseToMaxDose()
        {
            var dvh = DvhFactory.Linear("PTV", minDoseGy: 55.0, maxDoseGy: 65.0);

            // D2% should be very close to the maximum dose (65 Gy)
            double d2 = DvhCalculator.GetDoseAtVolume(dvh, 2.0);
            Assert.That(d2, Is.EqualTo(64.8).Within(0.3));
        }

        [Test]
        public void GetDoseAtVolume_NullDvhData_ReturnsNaN()
        {
            double result = DvhCalculator.GetDoseAtVolume(null!, 50.0);
            Assert.That(double.IsNaN(result), Is.True);
        }

        [Test]
        public void GetDoseAtVolume_EmptyCurve_ReturnsNaN()
        {
            var dvh = new StructureDvhData { CumulativeDvh = System.Array.Empty<DvhDataPoint>() };
            double result = DvhCalculator.GetDoseAtVolume(dvh, 50.0);
            Assert.That(double.IsNaN(result), Is.True);
        }

        // ── GetVolumeAtDose ────────────────────────────────────────────────────

        [Test]
        public void GetVolumeAtDose_UniformDvh_Returns100PctBelowDose()
        {
            var dvh = DvhFactory.Uniform("Cord", uniformDoseGy: 40.0);

            // Any dose at or below the prescribed dose covers 100 % of the volume
            Assert.That(DvhCalculator.GetVolumeAtDose(dvh, 20.0), Is.EqualTo(100.0).Within(0.1));
        }

        [Test]
        public void GetVolumeAtDose_UniformDvh_Returns0PctAboveDose()
        {
            var dvh = DvhFactory.Uniform("Cord", uniformDoseGy: 40.0);

            // Dose above the maximum → 0 % volume
            Assert.That(DvhCalculator.GetVolumeAtDose(dvh, 45.0), Is.EqualTo(0.0).Within(0.1));
        }

        [Test]
        public void GetVolumeAtDose_LinearDvh_V20Gy()
        {
            // OAR receives 0–60 Gy uniformly → V20Gy = (60-20)/60 * 100 = 66.7 %
            var dvh = DvhFactory.OAR("Lung", maxDoseGy: 60.0);

            double v20 = DvhCalculator.GetVolumeAtDose(dvh, 20.0);
            Assert.That(v20, Is.EqualTo(66.7).Within(1.0));
        }

        [Test]
        public void GetVolumeAtDose_NullDvhData_ReturnsNaN()
        {
            double result = DvhCalculator.GetVolumeAtDose(null!, 30.0);
            Assert.That(double.IsNaN(result), Is.True);
        }
    }
}
