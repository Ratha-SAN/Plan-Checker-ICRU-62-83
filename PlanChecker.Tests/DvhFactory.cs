using System;
using PlanChecker.Logic.Models;

namespace PlanChecker.Tests
{
    /// <summary>
    /// Factory methods for building synthetic DVH data used across test classes.
    /// </summary>
    internal static class DvhFactory
    {
        /// <summary>
        /// Creates a uniform PTV DVH: every point receives exactly
        /// <paramref name="uniformDoseGy"/> Gy.
        /// (Dmin = Dmax = Dmean = the specified dose; D98%, D50%, D2% are all equal.)
        /// </summary>
        public static StructureDvhData Uniform(string id, double uniformDoseGy, double volumeCC = 100.0)
        {
            // A uniform DVH is a step function: 100 % volume up to the dose, then 0 %.
            var curve = new[]
            {
                new DvhDataPoint { DoseGy = 0.0,            VolumePercent = 100.0 },
                new DvhDataPoint { DoseGy = uniformDoseGy,  VolumePercent = 100.0 },
                new DvhDataPoint { DoseGy = uniformDoseGy + 0.001, VolumePercent = 0.0  },
            };
            return new StructureDvhData
            {
                StructureId   = id,
                VolumeCC      = volumeCC,
                MinDoseGy     = uniformDoseGy,
                MaxDoseGy     = uniformDoseGy,
                MeanDoseGy    = uniformDoseGy,
                CumulativeDvh = curve,
            };
        }

        /// <summary>
        /// Creates a trapezoidal PTV DVH that linearly ramps from
        /// <paramref name="minDoseGy"/> (100 % volume coverage) to
        /// <paramref name="maxDoseGy"/> (0 % coverage).
        /// <para>
        /// D(v%) = maxDoseGy − (v/100) × (maxDoseGy − minDoseGy),
        /// so D50% = (min + max) / 2.
        /// </para>
        /// </summary>
        public static StructureDvhData Linear(
            string id,
            double minDoseGy,
            double maxDoseGy,
            double volumeCC = 200.0,
            int    steps    = 101)
        {
            // Build in ascending-dose / descending-volume order as required by DvhCalculator.
            var curve = new DvhDataPoint[steps];
            for (int i = 0; i < steps; i++)
            {
                double t       = (double)i / (steps - 1);        // 0 → 1
                double doseGy  = minDoseGy + t * (maxDoseGy - minDoseGy);   // ascending
                double volPct  = 100.0 - t * 100.0;              // 100 % → 0 %
                curve[i] = new DvhDataPoint { DoseGy = doseGy, VolumePercent = volPct };
            }

            double meanGy = (minDoseGy + maxDoseGy) / 2.0;
            return new StructureDvhData
            {
                StructureId   = id,
                VolumeCC      = volumeCC,
                MinDoseGy     = minDoseGy,
                MaxDoseGy     = maxDoseGy,
                MeanDoseGy    = meanGy,
                CumulativeDvh = curve,
            };
        }

        /// <summary>
        /// Creates a simple OAR DVH where the structure receives
        /// doses linearly from 0 to <paramref name="maxDoseGy"/>.
        /// </summary>
        public static StructureDvhData OAR(string id, double maxDoseGy, double volumeCC = 500.0)
            => Linear(id, 0.0, maxDoseGy, volumeCC);
    }
}
