namespace PlanChecker.Logic.Models
{
    /// <summary>Outcome of a single plan quality check.</summary>
    public enum CheckStatus
    {
        /// <summary>Criterion met.</summary>
        Pass,
        /// <summary>Criterion not met but not a hard failure.</summary>
        Warning,
        /// <summary>Criterion not met – hard failure.</summary>
        Fail,
        /// <summary>Informational only.</summary>
        Info
    }
}
