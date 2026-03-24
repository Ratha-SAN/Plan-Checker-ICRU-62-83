using System;
using System.Collections.Generic;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Checks
{
    /// <summary>
    /// Organ-at-risk (OAR) dose-volume constraint checks based on QUANTEC guidelines
    /// and common clinical practice.
    /// <para>
    /// Structures are matched by substring in their Eclipse identifier (case-insensitive).
    /// Constraints assume conventional fractionation (≈ 2 Gy / fraction).
    /// </para>
    /// </summary>
    public static class OARChecks
    {
        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Iterates over all provided OAR DVH datasets, applies the constraint
        /// matching the structure name, and returns the aggregated results.
        /// </summary>
        public static List<CheckResult> RunChecks(IEnumerable<StructureDvhData> oarDataList)
        {
            var results = new List<CheckResult>();

            foreach (var oar in oarDataList)
            {
                if (oar == null) continue;

                string id = oar.StructureId?.ToUpperInvariant() ?? string.Empty;

                if (id.Contains("CORD") || id.Contains("SPINALCORD") || id.Contains("SPINE"))
                    results.Add(CheckSpinalCord(oar));
                else if (id.Contains("BRAINSTEM") || id.Contains("BRAIN_STEM"))
                    results.Add(CheckBrainstem(oar));
                else if (id.Contains("CHIASM") || id.Contains("OPTIC"))
                    results.Add(CheckOpticStructure(oar));
                else if (id.Contains("LENS"))
                    results.Add(CheckLens(oar));
                else if (id.Contains("LUNG"))
                    results.AddRange(CheckLung(oar));
                else if (id.Contains("HEART"))
                    results.AddRange(CheckHeart(oar));
                else if (id.Contains("PAROTID"))
                    results.Add(CheckParotid(oar));
                else if (id.Contains("RECTUM") || id.Contains("RECT"))
                    results.AddRange(CheckRectum(oar));
                else if (id.Contains("BLADDER"))
                    results.AddRange(CheckBladder(oar));
                else if (id.Contains("LIVER"))
                    results.AddRange(CheckLiver(oar));
                else if ((id.Contains("FEMUR") || id.Contains("FEMORAL")) &&
                         (id.Contains("HEAD") || id.EndsWith("_L") || id.EndsWith("_R") ||
                          id.StartsWith("L_") || id.StartsWith("R_")))
                    results.Add(CheckFemoralHead(oar));
                else if (id.Contains("KIDNEY"))
                    results.AddRange(CheckKidney(oar));
            }

            return results;
        }

        // ── Individual OAR checks ──────────────────────────────────────────────

        /// <summary>Spinal cord: Dmax ≤ 45 Gy (hard limit 50 Gy).</summary>
        public static CheckResult CheckSpinalCord(StructureDvhData cord)
        {
            double dmax = cord.MaxDoseGy;
            bool good = dmax <= 45.0;
            bool acceptable = dmax <= 50.0;

            return new CheckResult
            {
                Name = $"Spinal Cord ({cord.StructureId}) – Dmax",
                Standard = "QUANTEC",
                Status = good ? CheckStatus.Pass : (acceptable ? CheckStatus.Warning : CheckStatus.Fail),
                ActualValue = Math.Round(dmax, 2),
                Limit = 45.0,
                Unit = "Gy",
                Message = good
                    ? $"Spinal cord Dmax = {dmax:F2} Gy (≤ 45 Gy)"
                    : acceptable
                        ? $"Spinal cord Dmax = {dmax:F2} Gy (45–50 Gy – approaching hard limit)"
                        : $"Spinal cord Dmax = {dmax:F2} Gy (> 50 Gy – EXCEEDS HARD LIMIT)"
            };
        }

        /// <summary>Brainstem: Dmax ≤ 54 Gy.</summary>
        public static CheckResult CheckBrainstem(StructureDvhData brainstem)
        {
            double dmax = brainstem.MaxDoseGy;
            bool pass = dmax <= 54.0;

            return new CheckResult
            {
                Name = $"Brainstem ({brainstem.StructureId}) – Dmax",
                Standard = "QUANTEC",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(dmax, 2),
                Limit = 54.0,
                Unit = "Gy",
                Message = pass
                    ? $"Brainstem Dmax = {dmax:F2} Gy (≤ 54 Gy)"
                    : $"Brainstem Dmax = {dmax:F2} Gy (> 54 Gy – FAIL)"
            };
        }

        /// <summary>Optic chiasm / optic nerves: Dmax ≤ 54 Gy.</summary>
        public static CheckResult CheckOpticStructure(StructureDvhData optic)
        {
            double dmax = optic.MaxDoseGy;
            bool pass = dmax <= 54.0;

            return new CheckResult
            {
                Name = $"Optic Structure ({optic.StructureId}) – Dmax",
                Standard = "QUANTEC",
                Status = pass ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(dmax, 2),
                Limit = 54.0,
                Unit = "Gy",
                Message = pass
                    ? $"{optic.StructureId} Dmax = {dmax:F2} Gy (≤ 54 Gy)"
                    : $"{optic.StructureId} Dmax = {dmax:F2} Gy (> 54 Gy – FAIL)"
            };
        }

        /// <summary>Lens: Dmax ≤ 5 Gy (cataract risk at &gt; 5 Gy).</summary>
        public static CheckResult CheckLens(StructureDvhData lens)
        {
            double dmax = lens.MaxDoseGy;
            bool good = dmax <= 5.0;
            bool acceptable = dmax <= 10.0;

            return new CheckResult
            {
                Name = $"Lens ({lens.StructureId}) – Dmax",
                Standard = "QUANTEC",
                Status = good ? CheckStatus.Pass : (acceptable ? CheckStatus.Warning : CheckStatus.Fail),
                ActualValue = Math.Round(dmax, 2),
                Limit = 5.0,
                Unit = "Gy",
                Message = good
                    ? $"Lens Dmax = {dmax:F2} Gy (≤ 5 Gy)"
                    : acceptable
                        ? $"Lens Dmax = {dmax:F2} Gy (> 5 Gy – cataract risk, review shielding)"
                        : $"Lens Dmax = {dmax:F2} Gy (> 10 Gy – high cataract risk)"
            };
        }

        /// <summary>
        /// Lung: V20Gy ≤ 35 % (pneumonitis risk) and mean dose ≤ 20 Gy.
        /// </summary>
        public static List<CheckResult> CheckLung(StructureDvhData lung)
        {
            var results = new List<CheckResult>();

            double v20 = DvhCalculator.GetVolumeAtDose(lung, 20.0);
            if (!double.IsNaN(v20))
            {
                results.Add(new CheckResult
                {
                    Name = $"Lung ({lung.StructureId}) – V20Gy",
                    Standard = "QUANTEC",
                    Status = v20 <= 35.0 ? CheckStatus.Pass : CheckStatus.Fail,
                    ActualValue = Math.Round(v20, 1),
                    Limit = 35.0,
                    Unit = "%",
                    Message = v20 <= 35.0
                        ? $"Lung V20Gy = {v20:F1}% (≤ 35%)"
                        : $"Lung V20Gy = {v20:F1}% (> 35% – elevated pneumonitis risk)"
                });
            }

            double mean = lung.MeanDoseGy;
            results.Add(new CheckResult
            {
                Name = $"Lung ({lung.StructureId}) – Mean Dose",
                Standard = "QUANTEC",
                Status = mean <= 20.0 ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(mean, 2),
                Limit = 20.0,
                Unit = "Gy",
                Message = mean <= 20.0
                    ? $"Lung mean = {mean:F2} Gy (≤ 20 Gy)"
                    : $"Lung mean = {mean:F2} Gy (> 20 Gy – review)"
            });

            return results;
        }

        /// <summary>
        /// Heart: mean dose ≤ 26 Gy and V30Gy ≤ 46 %.
        /// </summary>
        public static List<CheckResult> CheckHeart(StructureDvhData heart)
        {
            var results = new List<CheckResult>();

            double mean = heart.MeanDoseGy;
            results.Add(new CheckResult
            {
                Name = $"Heart ({heart.StructureId}) – Mean Dose",
                Standard = "QUANTEC",
                Status = mean <= 26.0 ? CheckStatus.Pass : CheckStatus.Fail,
                ActualValue = Math.Round(mean, 2),
                Limit = 26.0,
                Unit = "Gy",
                Message = mean <= 26.0
                    ? $"Heart mean = {mean:F2} Gy (≤ 26 Gy)"
                    : $"Heart mean = {mean:F2} Gy (> 26 Gy – pericarditis risk)"
            });

            double v30 = DvhCalculator.GetVolumeAtDose(heart, 30.0);
            if (!double.IsNaN(v30))
            {
                results.Add(new CheckResult
                {
                    Name = $"Heart ({heart.StructureId}) – V30Gy",
                    Standard = "QUANTEC",
                    Status = v30 <= 46.0 ? CheckStatus.Pass : CheckStatus.Warning,
                    ActualValue = Math.Round(v30, 1),
                    Limit = 46.0,
                    Unit = "%",
                    Message = v30 <= 46.0
                        ? $"Heart V30Gy = {v30:F1}% (≤ 46%)"
                        : $"Heart V30Gy = {v30:F1}% (> 46% – review)"
                });
            }

            return results;
        }

        /// <summary>
        /// Parotid gland: mean dose ≤ 26 Gy (bilateral, to reduce xerostomia risk).
        /// </summary>
        public static CheckResult CheckParotid(StructureDvhData parotid)
        {
            double mean = parotid.MeanDoseGy;
            bool good = mean <= 26.0;
            bool acceptable = mean <= 32.0;

            return new CheckResult
            {
                Name = $"Parotid ({parotid.StructureId}) – Mean Dose",
                Standard = "QUANTEC",
                Status = good ? CheckStatus.Pass : (acceptable ? CheckStatus.Warning : CheckStatus.Fail),
                ActualValue = Math.Round(mean, 2),
                Limit = 26.0,
                Unit = "Gy",
                Message = good
                    ? $"Parotid mean = {mean:F2} Gy (≤ 26 Gy – low xerostomia risk)"
                    : acceptable
                        ? $"Parotid mean = {mean:F2} Gy (26–32 Gy – moderate xerostomia risk)"
                        : $"Parotid mean = {mean:F2} Gy (> 32 Gy – high xerostomia risk)"
            };
        }

        /// <summary>
        /// Rectum (prostate RT): V50Gy ≤ 50 %, V60Gy ≤ 35 %, V65Gy ≤ 25 %, V70Gy ≤ 20 %.
        /// </summary>
        public static List<CheckResult> CheckRectum(StructureDvhData rectum)
        {
            var results = new List<CheckResult>();

            var constraints = new[]
            {
                (DoseGy: 50.0, MaxVolPct: 50.0),
                (DoseGy: 60.0, MaxVolPct: 35.0),
                (DoseGy: 65.0, MaxVolPct: 25.0),
                (DoseGy: 70.0, MaxVolPct: 20.0),
            };

            foreach (var (doseGy, maxVol) in constraints)
            {
                double v = DvhCalculator.GetVolumeAtDose(rectum, doseGy);
                if (double.IsNaN(v)) continue;

                results.Add(new CheckResult
                {
                    Name = $"Rectum ({rectum.StructureId}) – V{doseGy:F0}Gy",
                    Standard = "QUANTEC",
                    Status = v <= maxVol ? CheckStatus.Pass : CheckStatus.Fail,
                    ActualValue = Math.Round(v, 1),
                    Limit = maxVol,
                    Unit = "%",
                    Message = v <= maxVol
                        ? $"Rectum V{doseGy:F0}Gy = {v:F1}% (≤ {maxVol}%)"
                        : $"Rectum V{doseGy:F0}Gy = {v:F1}% (> {maxVol}% – FAIL)"
                });
            }

            return results;
        }

        /// <summary>
        /// Bladder (prostate RT): V50Gy ≤ 50 %, V65Gy ≤ 25 %.
        /// </summary>
        public static List<CheckResult> CheckBladder(StructureDvhData bladder)
        {
            var results = new List<CheckResult>();

            var constraints = new[]
            {
                (DoseGy: 50.0, MaxVolPct: 50.0),
                (DoseGy: 65.0, MaxVolPct: 25.0),
            };

            foreach (var (doseGy, maxVol) in constraints)
            {
                double v = DvhCalculator.GetVolumeAtDose(bladder, doseGy);
                if (double.IsNaN(v)) continue;

                results.Add(new CheckResult
                {
                    Name = $"Bladder ({bladder.StructureId}) – V{doseGy:F0}Gy",
                    Standard = "QUANTEC",
                    Status = v <= maxVol ? CheckStatus.Pass : CheckStatus.Fail,
                    ActualValue = Math.Round(v, 1),
                    Limit = maxVol,
                    Unit = "%",
                    Message = v <= maxVol
                        ? $"Bladder V{doseGy:F0}Gy = {v:F1}% (≤ {maxVol}%)"
                        : $"Bladder V{doseGy:F0}Gy = {v:F1}% (> {maxVol}% – FAIL)"
                });
            }

            return results;
        }

        /// <summary>
        /// Liver: V30Gy ≤ 60 % and mean dose ≤ 28 Gy (RILD risk).
        /// </summary>
        public static List<CheckResult> CheckLiver(StructureDvhData liver)
        {
            var results = new List<CheckResult>();

            double v30 = DvhCalculator.GetVolumeAtDose(liver, 30.0);
            if (!double.IsNaN(v30))
            {
                results.Add(new CheckResult
                {
                    Name = $"Liver ({liver.StructureId}) – V30Gy",
                    Standard = "QUANTEC",
                    Status = v30 <= 60.0 ? CheckStatus.Pass : CheckStatus.Fail,
                    ActualValue = Math.Round(v30, 1),
                    Limit = 60.0,
                    Unit = "%",
                    Message = v30 <= 60.0
                        ? $"Liver V30Gy = {v30:F1}% (≤ 60%)"
                        : $"Liver V30Gy = {v30:F1}% (> 60% – RILD risk)"
                });
            }

            double mean = liver.MeanDoseGy;
            results.Add(new CheckResult
            {
                Name = $"Liver ({liver.StructureId}) – Mean Dose",
                Standard = "QUANTEC",
                Status = mean <= 28.0 ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(mean, 2),
                Limit = 28.0,
                Unit = "Gy",
                Message = mean <= 28.0
                    ? $"Liver mean = {mean:F2} Gy (≤ 28 Gy)"
                    : $"Liver mean = {mean:F2} Gy (> 28 Gy – review)"
            });

            return results;
        }

        /// <summary>Femoral head: Dmax ≤ 50 Gy (fracture risk).</summary>
        public static CheckResult CheckFemoralHead(StructureDvhData femur)
        {
            double dmax = femur.MaxDoseGy;
            bool good = dmax <= 50.0;
            bool acceptable = dmax <= 52.0;

            return new CheckResult
            {
                Name = $"Femoral Head ({femur.StructureId}) – Dmax",
                Standard = "QUANTEC",
                Status = good ? CheckStatus.Pass : (acceptable ? CheckStatus.Warning : CheckStatus.Fail),
                ActualValue = Math.Round(dmax, 2),
                Limit = 50.0,
                Unit = "Gy",
                Message = good
                    ? $"Femoral head Dmax = {dmax:F2} Gy (≤ 50 Gy)"
                    : acceptable
                        ? $"Femoral head Dmax = {dmax:F2} Gy (> 50 Gy – fracture risk)"
                        : $"Femoral head Dmax = {dmax:F2} Gy (> 52 Gy – high fracture risk)"
            };
        }

        /// <summary>
        /// Kidney: mean dose ≤ 18 Gy and V20Gy ≤ 32 %.
        /// </summary>
        public static List<CheckResult> CheckKidney(StructureDvhData kidney)
        {
            var results = new List<CheckResult>();

            double mean = kidney.MeanDoseGy;
            results.Add(new CheckResult
            {
                Name = $"Kidney ({kidney.StructureId}) – Mean Dose",
                Standard = "QUANTEC",
                Status = mean <= 18.0 ? CheckStatus.Pass : CheckStatus.Warning,
                ActualValue = Math.Round(mean, 2),
                Limit = 18.0,
                Unit = "Gy",
                Message = mean <= 18.0
                    ? $"Kidney mean = {mean:F2} Gy (≤ 18 Gy)"
                    : $"Kidney mean = {mean:F2} Gy (> 18 Gy – review)"
            });

            double v20 = DvhCalculator.GetVolumeAtDose(kidney, 20.0);
            if (!double.IsNaN(v20))
            {
                results.Add(new CheckResult
                {
                    Name = $"Kidney ({kidney.StructureId}) – V20Gy",
                    Standard = "QUANTEC",
                    Status = v20 <= 32.0 ? CheckStatus.Pass : CheckStatus.Warning,
                    ActualValue = Math.Round(v20, 1),
                    Limit = 32.0,
                    Unit = "%",
                    Message = v20 <= 32.0
                        ? $"Kidney V20Gy = {v20:F1}% (≤ 32%)"
                        : $"Kidney V20Gy = {v20:F1}% (> 32% – review)"
                });
            }

            return results;
        }
    }
}
