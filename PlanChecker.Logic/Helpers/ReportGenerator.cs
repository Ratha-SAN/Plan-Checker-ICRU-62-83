using System.Collections.Generic;
using System.Text;
using PlanChecker.Logic.Models;

namespace PlanChecker.Logic.Helpers
{
    /// <summary>
    /// Generates a self-contained, styled HTML report from a <see cref="PlanCheckReport"/>.
    /// </summary>
    public static class ReportGenerator
    {
        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a complete HTML document for the supplied plan-check report.
        /// </summary>
        public static string GenerateHtmlReport(PlanCheckReport report)
        {
            // Collect every result so we can summarise
            var all = new List<CheckResult>();
            all.AddRange(report.PlanSetupResults);
            all.AddRange(report.ICRU62Results);
            all.AddRange(report.ICRU83Results);
            all.AddRange(report.OARResults);

            int passCount = 0, warnCount = 0, failCount = 0;
            foreach (var r in all)
            {
                switch (r.Status)
                {
                    case CheckStatus.Pass:    passCount++; break;
                    case CheckStatus.Warning: warnCount++; break;
                    case CheckStatus.Fail:    failCount++; break;
                }
            }

            bool overallPass = failCount == 0;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"  <title>Plan Checker – {Enc(report.PatientId)} / {Enc(report.PlanId)}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine(Css());
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // ── Header ──────────────────────────────────────────────────────
            sb.AppendLine("<header>");
            sb.AppendLine("  <h1>Plan Checker Report</h1>");
            sb.AppendLine("  <h2>ICRU 62 &amp; 83 Compliance Analysis</h2>");
            sb.AppendLine($"  <p class=\"subtitle\">Generated: {Enc(report.GeneratedAt)}</p>");
            sb.AppendLine("</header>");

            // ── Summary banner ───────────────────────────────────────────────
            string bannerClass = overallPass ? "banner banner-pass" : "banner banner-fail";
            sb.AppendLine($"<div class=\"{bannerClass}\">");
            sb.AppendLine($"  <span class=\"overall\">{(overallPass ? "✓ OVERALL PASS" : "✗ OVERALL FAIL")}</span>");
            sb.AppendLine($"  <span class=\"badge badge-pass\">✓ Pass: {passCount}</span>");
            sb.AppendLine($"  <span class=\"badge badge-warn\">⚠ Warning: {warnCount}</span>");
            sb.AppendLine($"  <span class=\"badge badge-fail\">✗ Fail: {failCount}</span>");
            sb.AppendLine("</div>");

            // ── Plan information ─────────────────────────────────────────────
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h3>Plan Information</h3>");
            sb.AppendLine("  <table class=\"info\">");
            InfoRow(sb, "Patient ID",         report.PatientId);
            InfoRow(sb, "Patient Name",       report.PatientName);
            InfoRow(sb, "Course",             report.CourseId);
            InfoRow(sb, "Plan ID",            report.PlanId);
            InfoRow(sb, "Prescription Dose",  $"{report.PrescriptionDoseGy:F2} Gy");
            InfoRow(sb, "Dose per Fraction",  $"{report.DosePerFractionGy:F2} Gy");
            InfoRow(sb, "Fractions",          report.NumberOfFractions.ToString());
            InfoRow(sb, "Technique",          report.PlanningTechnique);
            InfoRow(sb, "Algorithm",          report.CalculationAlgorithm);
            sb.AppendLine("  </table>");
            sb.AppendLine("</section>");

            // ── Check-result sections ────────────────────────────────────────
            AppendSection(sb, "Plan Setup", null, report.PlanSetupResults);
            AppendSection(sb, "ICRU Report 62",
                "Prescribing, Recording and Reporting Photon Beam Therapy (ICRU 62 / 50)",
                report.ICRU62Results);
            AppendSection(sb, "ICRU Report 83",
                "Prescribing, Recording and Reporting Intensity-Modulated Photon-Beam RT (ICRU 83)",
                report.ICRU83Results);
            AppendSection(sb, "Organs at Risk (OAR)",
                "QUANTEC dose–volume constraints",
                report.OARResults);

            // ── Footer ───────────────────────────────────────────────────────
            sb.AppendLine("<footer>");
            sb.AppendLine("  <p>Plan Checker – ICRU 62 &amp; 83 | Varian Eclipse ESAPI</p>");
            sb.AppendLine("  <p><small>For quality-assurance purposes only. " +
                          "All clinical decisions must be made by a qualified medical professional.</small></p>");
            sb.AppendLine("</footer>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static void AppendSection(
            StringBuilder sb,
            string title,
            string? subtitle,
            List<CheckResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine($"  <h3>{Enc(title)}</h3>");

            if (!string.IsNullOrEmpty(subtitle))
                sb.AppendLine($"  <p class=\"subtitle\">{Enc(subtitle)}</p>");

            sb.AppendLine("  <table class=\"results\">");
            sb.AppendLine("    <thead>");
            sb.AppendLine("      <tr><th>Check</th><th>Status</th><th>Value</th><th>Limit</th><th>Details</th></tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (var r in results)
            {
                string rowCss = r.Status switch
                {
                    CheckStatus.Pass    => "row-pass",
                    CheckStatus.Warning => "row-warn",
                    CheckStatus.Fail    => "row-fail",
                    _                  => "row-info",
                };

                string statusHtml = r.Status switch
                {
                    CheckStatus.Pass    => "<span class=\"s-pass\">✓ PASS</span>",
                    CheckStatus.Warning => "<span class=\"s-warn\">⚠ WARN</span>",
                    CheckStatus.Fail    => "<span class=\"s-fail\">✗ FAIL</span>",
                    _                  => "<span class=\"s-info\">ℹ INFO</span>",
                };

                string val   = r.ActualValue.HasValue ? $"{r.ActualValue:F1} {Enc(r.Unit)}" : "–";
                string limit = r.Limit.HasValue       ? $"{r.Limit:F1} {Enc(r.Unit)}"       : "–";

                sb.AppendLine($"      <tr class=\"{rowCss}\">");
                sb.AppendLine($"        <td>{Enc(r.Name)}</td>");
                sb.AppendLine($"        <td>{statusHtml}</td>");
                sb.AppendLine($"        <td>{val}</td>");
                sb.AppendLine($"        <td>{limit}</td>");
                sb.AppendLine($"        <td>{Enc(r.Message)}</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</section>");
        }

        private static void InfoRow(StringBuilder sb, string label, string value)
            => sb.AppendLine($"    <tr><th>{Enc(label)}</th><td>{Enc(value)}</td></tr>");

        /// <summary>Minimal HTML entity encoding.</summary>
        private static string Enc(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text!
                .Replace("&",  "&amp;")
                .Replace("<",  "&lt;")
                .Replace(">",  "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'",  "&#39;");
        }

        private static string Css() => @"
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  body   { font-family: 'Segoe UI', Arial, sans-serif; background: #f4f6f9; color: #333; font-size: 14px; }
  header { background: #1a3a5c; color: #fff; padding: 18px 36px; }
  header h1 { font-size: 22px; }
  header h2 { font-size: 16px; font-weight: 400; opacity: .85; margin-top: 4px; }
  header .subtitle, footer p { font-size: 12px; opacity: .7; margin-top: 6px; }
  .banner { display: flex; align-items: center; gap: 12px; padding: 12px 36px;
            border-left: 6px solid transparent; flex-wrap: wrap; }
  .banner-pass { background: #d4edda; border-color: #28a745; }
  .banner-fail { background: #f8d7da; border-color: #dc3545; }
  .overall { font-size: 18px; font-weight: 700; flex: 1; }
  .badge { padding: 3px 10px; border-radius: 10px; font-weight: 600; font-size: 13px; }
  .badge-pass { background: #28a745; color: #fff; }
  .badge-warn { background: #ffc107; color: #333; }
  .badge-fail { background: #dc3545; color: #fff; }
  .card  { background: #fff; margin: 16px 36px; padding: 18px 22px;
           border-radius: 6px; box-shadow: 0 1px 4px rgba(0,0,0,.12); }
  .card h3 { font-size: 16px; color: #1a3a5c; border-bottom: 2px solid #1a3a5c;
             padding-bottom: 6px; margin-bottom: 10px; }
  .card .subtitle { font-size: 12px; color: #666; font-style: italic; margin-bottom: 10px; }
  table.info  { width: 100%; border-collapse: collapse; }
  table.info th { text-align: left; padding: 5px 10px; width: 200px; color: #555; font-weight: 600; }
  table.info td { padding: 5px 10px; }
  table.info tr:nth-child(even) { background: #f8f9fa; }
  table.results { width: 100%; border-collapse: collapse; font-size: 13px; }
  table.results thead th { background: #1a3a5c; color: #fff; padding: 7px 10px; text-align: left; }
  table.results td { padding: 6px 10px; border-bottom: 1px solid #eee; }
  .row-pass td { background: #f6fff8; }
  .row-warn td { background: #fffef0; }
  .row-fail td { background: #fff5f5; }
  .row-info td { background: #f0f7ff; }
  .s-pass { color: #1e7e34; font-weight: 700; }
  .s-warn { color: #856404; font-weight: 700; }
  .s-fail { color: #c82333; font-weight: 700; }
  .s-info { color: #0056b3; font-weight: 700; }
  footer  { text-align: center; padding: 16px 36px; font-size: 12px; color: #666;
            border-top: 1px solid #dde; margin-top: 10px; }
";
    }
}
