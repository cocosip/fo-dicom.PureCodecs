# Test Fixture Notes

The JPEG baseline tests currently use synthetic DICOM pixel-data fixtures built in
`DicomPixelDataFixtures`.

External Efferent/fo-dicom sample files are referenced through
`ExternalFixtureCatalog`. The files are not copied into this repository because
their source roots are developer-machine specific and their redistribution
status must be checked before committing any sample data.

- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance`
- `<FO_DICOM_SOURCE_ROOT>\Tests`

Path resolution order:

1. `FO_DICOM_CODECS_SOURCE_ROOT` and `FO_DICOM_SOURCE_ROOT` environment variables.
2. Known local checkout fallbacks under `D:\Code\dotnet-source`.
3. Known local checkout fallbacks under `D:\Code\ts\dicom-ts\source-code`.

Before any external sample is committed, confirm that its license permits
redistribution in this repository. If redistribution is not allowed, reference it
from a local checkout or test data package instead.

The current external fixture matrix includes:

- Efferent unit samples: `test8bits.dcm`, `test16bits.dcm`.
- Efferent acceptance samples for RLE, JPEG, JPEG-LS, and JPEG 2000.
- fo-dicom JPEG rendering regression samples such as `GH538-jpeg1.dcm` and
  `GH538-jpeg14sv1.dcm`, when `<FO_DICOM_SOURCE_ROOT>` is available.
