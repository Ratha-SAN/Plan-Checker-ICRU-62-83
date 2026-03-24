# Plan Checker – ICRU 62 & 83

A **Varian Eclipse ESAPI** script that checks treatment plans for compliance
with **ICRU Report 62** (*Prescribing, Recording and Reporting Photon Beam
Therapy*) and **ICRU Report 83** (*IMRT*).

---

## Features

| Category | Checks |
|---|---|
| **Plan Setup** | Plan ID, prescription consistency, dose calculated, algorithm, normalisation, dose-matrix resolution |
| **ICRU 62** | GTV/CTV/PTV structure existence, D95% ≥ 95 % of Rx, PTV Dmax ≤ 107 %, ICRU reference-point dose (95–107 %), global hot-spot ≤ 110 % |
| **ICRU 83** | D98% ≥ 95 % (near-minimum), D2% ≤ 107 % (near-maximum), D50% 95–107 %, mean dose, Homogeneity Index HI = (D2%−D98%)/D50%, Conformity Index CI = V_Rx / V_PTV |
| **OAR constraints** | Spinal cord, brainstem, optic structures, lens, lung, heart, parotid, rectum, bladder, liver, femoral heads, kidneys (QUANTEC) |

The script generates a **self-contained, colour-coded HTML report** and opens
it in the system browser, plus shows a summary dialog inside Eclipse.

---

## Project Structure

```
PlanChecker.sln
│
├── PlanChecker.Logic/          # .NET Standard 2.0 – no ESAPI dependency
│   ├── Models/
│   │   ├── CheckStatus.cs
│   │   ├── CheckResult.cs
│   │   ├── DvhDataPoint.cs
│   │   ├── StructureDvhData.cs
│   │   └── PlanCheckReport.cs
│   ├── Checks/
│   │   ├── ICRU62Checks.cs
│   │   ├── ICRU83Checks.cs
│   │   ├── PlanSetupChecks.cs
│   │   └── OARChecks.cs
│   └── Helpers/
│       ├── DvhCalculator.cs
│       └── ReportGenerator.cs
│
├── PlanChecker/                # .NET Framework 4.6.1 – Eclipse ESAPI entry point
│   └── Script.cs
│
└── PlanChecker.Tests/          # .NET 8 – 82 NUnit unit tests
    ├── DvhFactory.cs
    ├── DvhCalculatorTests.cs
    ├── ICRU62ChecksTests.cs
    ├── ICRU83ChecksTests.cs
    ├── PlanSetupChecksTests.cs
    ├── OARChecksTests.cs
    └── ReportGeneratorTests.cs
```

---

## Deployment to Eclipse

1. **Copy ESAPI DLLs** from your Eclipse installation into
   `PlanChecker/esapi/`:
   ```
   C:\Program Files (x86)\Varian\RTM\<version>\esapi\API\
     VMS.TPS.Common.Model.API.dll
     VMS.TPS.Common.Model.Types.dll
   ```
2. **Build** the solution in Visual Studio (Release | Any CPU).
3. **Copy** `PlanChecker.dll` and `PlanChecker.Logic.dll` from
   `PlanChecker/bin/Release/` to your Eclipse scripting folder.
4. In Eclipse, open a calculated plan and run the script via
   **Tools ▸ Scripts…**

---

## Running Tests

```bash
dotnet test PlanChecker.Tests/PlanChecker.Tests.csproj
```

All 82 tests pass without any Eclipse installation.

---

## References

- ICRU Report 50 – Prescribing, Recording and Reporting Photon Beam Therapy (1993)
- ICRU Report 62 – Prescribing, Recording and Reporting Photon Beam Therapy, Supplement to ICRU 50 (1999)
- ICRU Report 83 – Prescribing, Recording and Reporting Intensity-Modulated Photon-Beam Radiation Therapy (IMRT) (2010)
- Bentzen et al., QUANTEC guidelines (2010) – dose–volume constraints for OARs
