using NUnit.Framework;
using PlanChecker.Logic.Helpers;
using PlanChecker.Logic.Models;
using System.Collections.Generic;

namespace PlanChecker.Tests
{
    [TestFixture]
    public class ReportGeneratorTests
    {
        private static PlanCheckReport MakeReport(List<CheckResult>? icru62 = null,
                                                   List<CheckResult>? icru83 = null)
        {
            return new PlanCheckReport
            {
                PatientId            = "PT001",
                PatientName          = "Doe, John",
                PlanId               = "IMRT_Head",
                CourseId             = "C1",
                PrescriptionDoseGy   = 60.0,
                DosePerFractionGy    = 2.0,
                NumberOfFractions    = 30,
                PlanningTechnique    = "IMRT",
                CalculationAlgorithm = "AAA_15.0.1",
                GeneratedAt          = "2026-01-01 12:00:00",
                PlanSetupResults     = new List<CheckResult>(),
                ICRU62Results        = icru62 ?? new List<CheckResult>(),
                ICRU83Results        = icru83 ?? new List<CheckResult>(),
                OARResults           = new List<CheckResult>(),
            };
        }

        [Test]
        public void GenerateHtmlReport_ReturnsNonEmptyString()
        {
            var report = MakeReport();
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GenerateHtmlReport_ContainsPatientId()
        {
            var report = MakeReport();
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("PT001"));
        }

        [Test]
        public void GenerateHtmlReport_ContainsPlanId()
        {
            var report = MakeReport();
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("IMRT_Head"));
        }

        [Test]
        public void GenerateHtmlReport_ContainsPrescriptionDose()
        {
            var report = MakeReport();
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("60.00 Gy"));
        }

        [Test]
        public void GenerateHtmlReport_ShowsOverallPass_WhenNoFailures()
        {
            var report = MakeReport(
                icru62: new List<CheckResult>
                {
                    new CheckResult { Name = "D95", Status = CheckStatus.Pass, Message = "OK" },
                }
            );
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("OVERALL PASS"));
        }

        [Test]
        public void GenerateHtmlReport_ShowsOverallFail_WhenThereAreFailures()
        {
            var report = MakeReport(
                icru62: new List<CheckResult>
                {
                    new CheckResult { Name = "D95", Status = CheckStatus.Fail, Message = "FAIL" },
                }
            );
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("OVERALL FAIL"));
        }

        [Test]
        public void GenerateHtmlReport_EncodesMaliciousCharacters()
        {
            var report = MakeReport();
            report.PatientId   = "<script>alert(1)</script>";
            report.PatientName = "O'Brien & \"Smith\"";

            string html = ReportGenerator.GenerateHtmlReport(report);

            Assert.That(html, Does.Not.Contain("<script>"));
            Assert.That(html, Does.Contain("&lt;script&gt;"));
            Assert.That(html, Does.Contain("O&#39;Brien &amp; &quot;Smith&quot;"));
        }

        [Test]
        public void GenerateHtmlReport_ContainsICRU62SectionTitle()
        {
            var report = MakeReport(icru62: new List<CheckResult>
            {
                new CheckResult { Name = "PTV D95", Status = CheckStatus.Pass, Message = "OK" }
            });
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("ICRU Report 62"));
        }

        [Test]
        public void GenerateHtmlReport_ContainsICRU83SectionTitle()
        {
            var report = MakeReport(icru83: new List<CheckResult>
            {
                new CheckResult { Name = "HI", Status = CheckStatus.Pass, Message = "OK" }
            });
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.Contain("ICRU Report 83"));
        }

        [Test]
        public void GenerateHtmlReport_EmptyReport_IsValidHtml()
        {
            var report = MakeReport();
            string html = ReportGenerator.GenerateHtmlReport(report);
            Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
            Assert.That(html, Does.Contain("</html>"));
        }
    }
}
