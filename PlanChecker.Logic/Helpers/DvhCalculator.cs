using System;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Helpers
{
    /// <summary>
    /// Utility methods for querying cumulative DVH data via linear interpolation.
    /// </summary>
    public static class DvhCalculator
    {
        /// <summary>
        /// Returns the dose (Gy) at which <paramref name="volumePercent"/>% of the
        /// structure volume is still covered – i.e. D(<paramref name="volumePercent"/>%).
        /// <para>
        /// For a cumulative DVH sorted by ascending dose / descending volume:
        /// D98% is the dose where 98 % of the structure volume receives at least
        /// that dose, i.e. the near-minimum dose per ICRU 83.
        /// </para>
        /// </summary>
        /// <param name="dvhData">Structure DVH data.</param>
        /// <param name="volumePercent">Volume percentage (0–100).</param>
        /// <returns>Interpolated dose in Gy, or <see cref="double.NaN"/> if unavailable.</returns>
        public static double GetDoseAtVolume(StructureDvhData dvhData, double volumePercent)
        {
            if (dvhData?.CumulativeDvh == null || dvhData.CumulativeDvh.Length == 0)
                return double.NaN;

            var dvh = dvhData.CumulativeDvh;

            // Boundary clamps
            if (volumePercent >= dvh[0].VolumePercent)
                return dvh[0].DoseGy;
            if (volumePercent <= dvh[dvh.Length - 1].VolumePercent)
                return dvh[dvh.Length - 1].DoseGy;

            // Linear interpolation: find the segment where volume crosses volumePercent
            for (int i = 0; i < dvh.Length - 1; i++)
            {
                double v1 = dvh[i].VolumePercent;
                double v2 = dvh[i + 1].VolumePercent;

                if (v1 >= volumePercent && v2 <= volumePercent)
                {
                    double dv = v1 - v2;
                    if (Math.Abs(dv) < 1e-10)
                        return dvh[i].DoseGy;

                    double t = (v1 - volumePercent) / dv;
                    return dvh[i].DoseGy + t * (dvh[i + 1].DoseGy - dvh[i].DoseGy);
                }
            }

            return double.NaN;
        }

        /// <summary>
        /// Returns the percentage of the structure volume receiving at least
        /// <paramref name="doseGy"/> Gray – i.e. V(<paramref name="doseGy"/>Gy).
        /// </summary>
        /// <param name="dvhData">Structure DVH data.</param>
        /// <param name="doseGy">Dose threshold in Gy.</param>
        /// <returns>Volume percentage (0–100), or <see cref="double.NaN"/> if unavailable.</returns>
        public static double GetVolumeAtDose(StructureDvhData dvhData, double doseGy)
        {
            if (dvhData?.CumulativeDvh == null || dvhData.CumulativeDvh.Length == 0)
                return double.NaN;

            var dvh = dvhData.CumulativeDvh;

            // Boundary clamps
            if (doseGy <= dvh[0].DoseGy)
                return 100.0;
            if (doseGy >= dvh[dvh.Length - 1].DoseGy)
                return 0.0;

            // Linear interpolation
            for (int i = 0; i < dvh.Length - 1; i++)
            {
                double d1 = dvh[i].DoseGy;
                double d2 = dvh[i + 1].DoseGy;

                if (d1 <= doseGy && d2 >= doseGy)
                {
                    double dd = d2 - d1;
                    if (Math.Abs(dd) < 1e-10)
                        return dvh[i].VolumePercent;

                    double t = (doseGy - d1) / dd;
                    return dvh[i].VolumePercent + t * (dvh[i + 1].VolumePercent - dvh[i].VolumePercent);
                }
            }

            return double.NaN;
        }
    }
}
