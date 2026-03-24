namespace PlanChecker.Logic.Models
{
    /// <summary>
    /// DVH and summary statistics for a single structure, as extracted from Eclipse.
    /// The <see cref="CumulativeDvh"/> array must be sorted by ascending dose
    /// (i.e. descending volume – as in a standard cumulative DVH curve).
    /// </summary>
    public class StructureDvhData
    {
        /// <summary>Structure identifier as shown in Eclipse.</summary>
        public string StructureId { get; set; } = string.Empty;

        /// <summary>Structure volume in cubic centimetres (cc).</summary>
        public double VolumeCC { get; set; }

        /// <summary>Minimum dose to the structure in Gy.</summary>
        public double MinDoseGy { get; set; }

        /// <summary>Maximum dose to the structure in Gy.</summary>
        public double MaxDoseGy { get; set; }

        /// <summary>Mean dose to the structure in Gy.</summary>
        public double MeanDoseGy { get; set; }

        /// <summary>
        /// Cumulative DVH data points, sorted by ascending dose / descending volume.
        /// Each point represents (dose Gy, % volume receiving ≥ that dose).
        /// </summary>
        public DvhDataPoint[] CumulativeDvh { get; set; } = System.Array.Empty<DvhDataPoint>();
    }
}
