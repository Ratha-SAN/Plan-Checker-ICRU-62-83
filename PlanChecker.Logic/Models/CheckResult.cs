namespace PlanChecker.Logic.Models
{
    /// <summary>
    /// Result of a single plan quality check, including the criterion name,
    /// pass/fail status, measured value and the applicable limit.
    /// </summary>
    public class CheckResult
    {
        /// <summary>Short, human-readable name of the check.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Outcome of the check.</summary>
        public CheckStatus Status { get; set; }

        /// <summary>Descriptive message explaining the outcome.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Measured value (optional).</summary>
        public double? ActualValue { get; set; }

        /// <summary>Criterion limit (optional).</summary>
        public double? Limit { get; set; }

        /// <summary>Unit string for ActualValue / Limit (e.g. "% of Rx", "Gy").</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Reporting standard this check belongs to (e.g. "ICRU 62").</summary>
        public string Standard { get; set; } = string.Empty;
    }
}
