namespace PlanChecker.Logic.Models
{
    /// <summary>
    /// A single point on a cumulative dose-volume histogram (DVH).
    /// The curve is sorted by ascending dose (descending volume).
    /// </summary>
    public struct DvhDataPoint
    {
        /// <summary>Dose in Gray (Gy).</summary>
        public double DoseGy { get; set; }

        /// <summary>Percentage of the structure volume receiving at least <see cref="DoseGy"/>.</summary>
        public double VolumePercent { get; set; }
    }
}
