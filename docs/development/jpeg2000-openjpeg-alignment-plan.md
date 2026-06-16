# JPEG 2000 OpenJPEG Alignment Plan

## Goal

Align classic JPEG 2000 `.90` and `.91` behavior with `fo-dicom.Codecs`
5.16.5.1/OpenJPEG for `D:\1.dcm` and future multi-layer fixtures. Do not tune
final file size by guesswork. Every repair must be tied to an OpenJPEG-visible
submodule behavior.

Binary equality and codestream frame size are terminal compatibility signals,
not the primary development driver. The implementation must first follow the
same encode pipeline as `fo-dicom.Codecs`/OpenJPEG, module by module. Only after
the pipeline stages line up should byte size, packet shape, and final binary
differences be used to locate remaining drift.

## OpenJPEG Encode Pipeline To Match

`fo-dicom.Codecs` 5.16.5.1 configures OpenJPEG classic JPEG 2000 encoding as
follows:

1. Create a raw J2K compressor (`CODEC_J2K`) and an `opj_image_t` from DICOM
   pixel metadata.
2. Set encoder defaults used by fo-dicom.Codecs:
   - `numresolution = 6`.
   - `cblockw_init = 64`, `cblockh_init = 64`.
   - `cp_rsiz = STD_RSIZ`.
   - `prog_order = DicomJpeg2000Params.ProgressionOrder`.
   - `subsampling_dx = 1`, `subsampling_dy = 1`.
   - `tcp_mct = 1` only for RGB when `AllowMCT` is true.
   - `irreversible = 1` only for JPEG 2000 Lossy when parameters request
     irreversible coding.
3. Build quality layers from `RateLevels` and `Rate`:
   - Add each `RateLevels[r]` while it is greater than `Rate`.
   - Add the target `Rate * BitsStored / BitsAllocated`.
   - For JPEG 2000 Lossless with positive `Rate`, append a final `0` rate layer
     for the lossless tail.
   - OpenJPEG later normalizes rates `<= 1.0` to `0.0`.
4. Copy DICOM samples into `opj_image_comp_t.data`:
   - `prec = BitsStored`, `bpp = BitsAllocated`, `sgnd = PixelRepresentation`
     unless `EncodeSignedPixelValuesAsUnsigned` is set.
   - 8-bit and 16-bit signed data are sign-extended according to `BitsStored`
     and `HighBit`.
   - Unsigned data is masked to `BitsStored` when `BitsStored < BitsAllocated`.
5. OpenJPEG tile/component processing:
   - Apply MCT only when the configured component transform requires it.
   - Use reversible 5/3 DWT for lossless and reversible paths.
   - Use irreversible 9/7 DWT for lossy irreversible paths. OpenJPEG performs
     this forward transform with `OPJ_FLOAT32`; managed code must not silently
     substitute a `double` forward path because low bit-plane Tier-1 payloads
     and pass rates drift.
   - Compute reversible no-quantization QCD or irreversible scalar-expounded
     QCD and band step sizes.
6. Tier-1 (`t1.c`) code-block encoding:
   - For irreversible paths, quantize each float32 transformed coefficient with
     `opj_lrintf((tiledp_f / band->stepsize) * (1 << T1_NMSEDEC_FRACBITS))`.
   - Scan code-blocks in OpenJPEG's stripe order.
   - Emit significance, magnitude-refinement, and cleanup passes.
   - Record cumulative pass `rate` and cumulative `distortiondec`.
   - `distortiondec` is based on the OpenJPEG `nmsedec` calculations and
     weighted by DWT norm, band level, orientation, transform type, and step
     size.
7. PCRD quality-layer allocation (`tcd.c`):
   - Convert requested rates to tile byte budgets in `j2k.c`.
   - For positive-rate layers, run threshold search over pass `dd/dr`.
   - For a `0` rate layer, include all remaining passes.
   - Verify candidate thresholds by encoding packets up to `layno + 1` and
     checking the cumulative packet byte count against the layer budget.
8. Tier-2 packet writing (`t2.c`):
   - Write LRCP packets for the selected progression order.
   - Preserve tag-tree inclusion, zero-bit-plane, pass-count, length-bit, and
     packet body contribution state across layers.
   - Match OpenJPEG 2.5.4 default packet-header behavior: unless
     `ENABLE_EMPTY_PACKET_OPTIMIZATION` is compiled in, a packet with no new
     code-block contribution still writes the packet-present bit and tag-tree
     header state. Do not replace it with a single `00` empty-packet byte.
9. Write the raw codestream markers (`SOC`, `SIZ`, `COD`, `QCD`, `COM`, `SOT`,
   `SOD`, `EOC`) and add the DICOM encapsulated item padding byte after EOC
   when the frame length is odd.

The managed implementation must be checked in that order. A final frame-size
match without matching the preceding stages is not sufficient, and a final
frame-size mismatch is not by itself the root cause.

## Current Baseline

Known OpenJPEG baselines generated from `D:\1.dcm`:

- `artifacts\fo-dicom-codecs-baseline\fo_dicom_codecs_j2k_lossless.dcm`
  - Codestream frame size: 173,228 bytes.
  - COD: `00 00 00 08 00 05 04 04 00 01`.
  - QCD: `40 80 88 88 90 88 88 90 88 88 90 88 88 90 88 88 90`.
- `artifacts\fo-dicom-codecs-baseline\fo_dicom_codecs_j2k_lossy.dcm`
  - Codestream frame size: 40,774 bytes.
  - COD: `00 00 00 07 00 05 04 04 00 00`.
  - QCD:
    `42 B7 20 B6 F0 B6 F0 B6 C0 AF 00 AF 00 AE E0 A7 50 A7 50 A7 68 90 05 90 05 90 47 97 D3 97 D3 97 62`.
  - COM: `Created by OpenJPEG version 2.5.4`.

`D:\1_transcoded\1_j2k_lossy.dcm` is not an OpenJPEG baseline for this fixture.
It has a 50,022-byte codestream frame, different COD/QCD payloads, and a COM
marker of `go-dicom-codec JPEG2000 encoder v1.0`. Keep it out of
`fo-dicom.Codecs` compatibility assertions.

Current managed output generated by
`artifacts\tool-regression-current-20260616-openjpeg-aligned` matches the
OpenJPEG baseline byte-for-byte for classic JPEG 2000:

- Lossless managed file SHA-256:
  `E07E0A745C50C5243A3F68A013F3FA82BDEFECCB6DD26589BA63EB0EEACE65F3`.
- Lossy managed file SHA-256:
  `1E62426443B734D9C6A6205F98EFEA3B4F4C795DDE2173787B5BB60D885047FA`.
- Both managed hashes equal the corresponding `fo-dicom.Codecs`/OpenJPEG
  baseline hashes.

The Efferent `test16bits.dcm` unit fixture also uses `fo-dicom.Codecs`
5.16.5.1/OpenJPEG as the lossy error-scale baseline. Native OpenJPEG default
JPEG 2000 Lossy encoding produced a 26,154-byte frame and a maximum decoded
signed-sample difference of 2,411. The managed default lossy path produced the
same frame size and the same maximum difference for that fixture, so the unit
test tolerance is pinned to 2,411 for evidence-backed OpenJPEG parity.

The Efferent RGB `test8bits.dcm` unit fixture is the current RGB lossy
cross-check. Native OpenJPEG default JPEG 2000 Lossy encoding produced a
39,326-byte frame and maximum decoded 8-bit sample difference of 6. The managed
RGB lossy path currently decodes within that native error scale, so the unit
test tolerance is pinned to 6. After the Tier-1 `nmsedec` LUT repair, the
managed RGB lossy path also writes a 39,326-byte frame with matching COD/QCD/SOT
and matching packet-layer distribution. This fixture stays in the regression
suite because decoded tolerance alone would not catch future RGB PCRD drift.

The final one-byte mismatch was not a standards-permitted output choice. It was
two OpenJPEG-alignment details that cancelled each other in some file-size
checks:

- Tier-2 empty quality-layer packets must follow OpenJPEG's default
  packet-present/tag-tree header path, not the optional empty-packet
  optimization.
- The DICOM frame item must include a trailing `00` padding byte after EOC when
  the logical codestream length is odd.

Frame-size matching is a compatibility signal, not a substitute for pixel
checks. The regression test now checks DICOM file size, encapsulated frame size,
logical EOC-terminated codestream length, and SOT `Psot`.

## Non-Negotiable Rules

- Do not add native codec execution, P/Invoke, or native fallbacks.
- Keep production projects on `netstandard2.0`.
- Do not satisfy tests by loosening tolerances or restoring size windows.
- Do not add artificial codestream padding or marker filler to match frame size;
  only the required DICOM encapsulated item padding after EOC is allowed.
- Do not claim multi-layer support just because COD layer count is greater than
  one.
- Keep lossy pixel tolerance for decoded image comparison, but continue to
  compare `D:\1.dcm` codestream frame size against the OpenJPEG baseline.

## Required Work Order

### 1. Baseline and Diagnostics

- [x] Keep `ToolsCompressionPlanTests.CompressAll_jpeg2000_classic_outputs_from_local_real_fixture_use_standard_codestream_and_match_reference_size`
  pointed at `artifacts\fo-dicom-codecs-baseline`, not `D:\1_transcoded`.
- [x] Keep repository-relative artifact path resolution so tests do not silently
  pass when run from `bin\Debug\net10.0`.
- [x] Add regression checks for the first encapsulated frame's DICOM frame size,
  EOC-terminated logical codestream length, and SOT `Psot`.
- [x] Add assertions that lossless and lossy managed COD/QCD/COM match the
  OpenJPEG baseline before investigating tile data.

### 2. Pixel Correctness First

- [x] Add a `D:\1.dcm` JPEG 2000 lossless decode assertion that transcodes the
  managed `.90` output back to raw and checks exact source-pixel equality.
- [x] Add a `D:\1.dcm` JPEG 2000 lossy decode assertion that compares signed
  16-bit samples, not bytes, against the source with the agreed tolerance.
- [x] If the image remains blurry while tolerance passes, add max/mean error
  diagnostics and fail on a tighter signed-sample metric for this fixture.

### 3. DWT and Level Shift

- [ ] Cross-check signed 16-bit level shift input values against OpenJPEG
  expectations for `PixelRepresentation=1`.
- [ ] Cross-check `opj_image_comp_t.data` population for 8-bit signed, 8-bit
  unsigned, 16-bit signed, and 16-bit unsigned paths against
  `DicomJpeg2000Codec.cs`.
- [ ] Add fixture-backed tests for reversible 5/3 coefficient placement on the
  888 x 459 odd-size image geometry.
- [ ] Add fixture-backed tests for irreversible 9/7 coefficient scale before
  quantization.

### 4. Quantization

- [x] Keep lossless QCD byte-for-byte equal to OpenJPEG.
- [x] Keep lossy QCD byte-for-byte equal to OpenJPEG.
- [x] Verify irreversible encoder and decoder derive Tier-1 bit-plane depth from
  encoded QCD step exponent plus guard bits, not from component precision alone.

### 5. Tier-1 Coding

- [x] Record cumulative pass `distortiondec` from managed Tier-1 encoding using
  the same OpenJPEG `nmsedec` model before using PCRD thresholds.
- [x] Compare code-block pass counts, pass lengths, and byte lengths against
  OpenJPEG-observable codestream data for representative LL, HL, LH, and HH
  blocks.
- [x] Do not change MQ/Tier-1 code just to alter final frame size unless a
  pass-level mismatch is identified.

### 6. Packet and Multi-Layer Behavior

- [x] Keep a regression test proving default multi-layer codestreams have real
  early quality-layer packet contributions.
- [x] Use OpenJPEG-compatible `Rate`/`RateLevels` quality-layer allocation based
  on cumulative pass `distortiondec` and `rate`, not heuristic pass weights.
- [x] Match `opj_tcd_makelayer` and `opj_tcd_rateallocate` behavior before
  treating exact codestream size as a blocker.
- [ ] Add a layer-truncated decode fixture or synthetic codestream that proves
  early layers are independently decodable at lower quality.

### 7. Final Compatibility

- [x] Re-run the targeted JPEG 2000 real fixture tests.
- [x] Re-run the full JPEG 2000 standard internal and DICOM integration test subsets.
- [ ] Re-run `dotnet test fo-dicom.PureCodecs.slnx` outside the sandbox.
- [x] Update this plan and `tool-compression-regression-log.md` with the final
  OpenJPEG-aligned sizes and pixel metrics.
