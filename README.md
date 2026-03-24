# Plan-Checker-ICRU-62-83

A **Varian Eclipse ESAPI** binary plug-in script that automatically checks a
radiotherapy treatment plan against the dose prescribing, recording, and
reporting guidelines of:

| Report | Year | Scope |
|--------|------|-------|
| **ICRU Report 62** | 1999 | 3D-CRT photon beam therapy (supplement to ICRU 50) |
| **ICRU Report 83** | 2010 | Photon-beam intensity-modulated radiation therapy (IMRT) |

---

## Checks performed

### ICRU Report 62 (3D-CRT)

| Parameter | Symbol | Criterion | Rationale |
|-----------|--------|-----------|-----------|
| Coverage dose | D95% | ≥ 95 % of Rx | ≥ 95 % of PTV volume receives full prescription dose |
| Hot-spot dose | D5% | ≤ 107 % of Rx | No more than 5 % of PTV exceeds 107 % of Rx |
| Near-maximum dose | Dmax (0.03 cc) | ≤ 107 % of Rx | Absolute point-dose hot spot within PTV |
| ICRU Reference Point | D50% (surrogate) | 95–107 % of Rx | Dose at the ICRU reference point (§3.3 of ICRU 62) |
| Volume coverage | V95% | ≥ 95 % of PTV | Fraction of PTV receiving ≥ 95 % of Rx dose |

### ICRU Report 83 (IMRT)

| Parameter | Symbol | Criterion | Rationale |
|-----------|--------|-----------|-----------|
| Near-minimum dose | D98% | ≥ 95 % of Rx | Near-minimum (cold-spot) dose within PTV |
| Near-maximum dose | D2% | ≤ 107 % of Rx | Near-maximum (hot-spot) dose within PTV |
| Median dose | D50% | 95–107 % of Rx | Median PTV dose should approximate prescribed dose |
| Homogeneity Index | HI = (D2%−D98%)/D50% | ≤ 0.10 | Dose uniformity within PTV (ideal: 0) |
| Conformity Index | Paddick CI | 0.90–1.50 | Spatial agreement between prescription isodose and PTV |

> **Tolerance limits** are based on the ICRU 50/62/83 consensus range of
> **95 %–107 %** of prescribed dose.  Individual clinics may apply tighter
> institutional constraints on top of these values.

---

## Requirements

| Item | Version |
|------|---------|
| Varian Eclipse | 15.x, 16.x, or 17.x |
| .NET Framework | 4.8 |
| ESAPI | VMS.TPS.Common.Model.API |
| Platform | x64 |

---

## Build instructions

1. Clone the repository.

2. Set the `VMS_TPS_API_PATH` environment variable to the ESAPI `API` folder
   of your Eclipse installation, e.g.:

   ```
   # Eclipse 16.1
   set VMS_TPS_API_PATH=C:\Program Files\Varian\RTM\16.1\esapi\API
   ```

   Alternatively, edit the `<HintPath>` entries in `PlanCheckerICRU.csproj`
   directly.

3. Build with Visual Studio 2019/2022 or the .NET CLI:

   ```
   dotnet build PlanCheckerICRU.csproj -c Release
   ```

4. The output DLL (`PlanCheckerICRU.dll`) is your compiled plug-in script.

---

## Installation & usage in Eclipse

1. Copy `PlanCheckerICRU.dll` to the Eclipse binary script directory
   (configured by your Varian system administrator).

2. Open a patient plan in Eclipse with a valid dose calculation.

3. Go to **Tools → Scripts** (or the script runner in your Eclipse version),
   select **PlanCheckerICRU**, and click **Run**.

4. A results window will appear showing pass/fail status for each ICRU
   criterion.  The background colour is **green** if all criteria pass,
   **red** if any criterion fails.

---

## Structure requirements

The script searches the plan's structure set for any structure whose:
* **DICOM type** is `PTV`, **or**
* **ID** starts with `PTV` (case-insensitive).

All matching PTV structures are evaluated.  A `BODY` or `EXTERNAL` structure
is used for the Paddick Conformity Index calculation (optional — CI will be
skipped if no such structure is found).

---

## References

* **ICRU Report 62** (1999). *Prescribing, Recording and Reporting Photon Beam
  Therapy* – Supplement to ICRU Report 50. International Commission on
  Radiation Units and Measurements, Bethesda, MD.

* **ICRU Report 83** (2010). *Prescribing, Recording, and Reporting
  Photon-Beam Intensity-Modulated Radiation Therapy (IMRT).*
  Journal of the ICRU, 10(1). [doi:10.1093/jicru/ndq002](https://doi.org/10.1093/jicru/ndq002)

* Paddick I (2000). A simple scoring ratio to index the conformity of
  radiosurgical treatment plans. *Journal of Neurosurgery*, 93(Suppl 3):219–222.

---

## License

This project is provided for educational and clinical research purposes.
Use in a clinical environment requires validation in accordance with your
institution's quality-assurance procedures and applicable regulations.
