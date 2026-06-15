# Tool Compression Regression Log

Tracked input: `D:\1.dcm`.

The input is a single-frame 888 x 459 MONOCHROME2 image with 16 bits allocated,
16 bits stored, signed pixels, and source transfer syntax Implicit VR Little
Endian.

## 2026-06-15

### PureCodecs Tool Reproduction

`fo-dicom.PureCodecs.Tools` produced successful outputs for RLE Lossless, JPEG
Lossless Process 14, JPEG Lossless Process 14 SV1, JPEG-LS Lossless, JPEG-LS
Near-Lossless, JPEG 2000 Lossless, JPEG 2000 Lossy, HTJ2K Lossless, HTJ2K
Lossless RPCL, and HTJ2K Lossy.

JPEG Baseline Process 1 and JPEG Extended Process 2/4 were reported as
unsupported because the input is 16-bit and the current managed sequential DCT
path supports only 8-bit input.

### RLE Lossless

Reference generated with `fo-dicom.Codecs` 5.16.5.1:

- Reference file size: 416,722 bytes.
- Reference RLE frame size: 412,718 bytes.

PureCodecs output:

- Pure file size: 416,750 bytes.
- Pure RLE frame size: 412,746 bytes.

The 28-byte file-size difference is an RLE byte-stream size difference, not a
DICOM metadata, encapsulation, or pixel compatibility failure. Both outputs have
the same transfer syntax UID, image geometry, pixel metadata, frame count, RLE
segment count, and RLE segment offsets. Decoding either output back to Explicit
VR Little Endian reproduces the source frame byte-for-byte.

Regression coverage:

- `ToolsCompressionPlanTests.CompressAll_rle_output_from_local_real_fixture_round_trips_exactly`
- `ToolsCompressionPlanTests.CompressAll_outputs_from_local_real_fixture_decode_and_render`

### JPEG Lossless Process 14 and Process 14 SV1

Reference preserved from the `fo-dicom.Codecs` output in `D:\1_transcoded`:

- Reference Process 14 file size: 238,540 bytes.
- Reference Process 14 SV1 file size: 238,540 bytes.
- Reference compressed frame size: 234,122 bytes for both transfer syntaxes.

Initial PureCodecs output:

- Pure Process 14 file size: 501,383 bytes.
- Pure Process 14 SV1 file size: 501,383 bytes.
- Pure compressed frame size: 497,377 bytes for both transfer syntaxes.

Intermediate PureCodecs output after switching away from the fixed 8-bit/category
table:

- Pure Process 14 file size: 222,028 bytes.
- Pure Process 14 SV1 file size: 222,028 bytes.
- Pure compressed frame size: 218,022 bytes for both transfer syntaxes.

Repaired PureCodecs output:

- Pure Process 14 file size: 238,128 bytes.
- Pure Process 14 SV1 file size: 238,128 bytes.
- Pure compressed frame size: 234,122 bytes for both transfer syntaxes.

The initial larger PureCodecs JPEG lossless bitstream was caused by writing a
fixed 8-bit/category DC Huffman table. The later smaller bitstream was also a
regression against the real `D:\1_transcoded` baseline: it omitted the APP0/JFIF
segment and used a shorter optimized DHT instead of the extended lossless DC
Huffman table emitted by `fo-dicom.Codecs` for this 16-bit image. PureCodecs now
writes the APP0/JFIF segment and the same extended 0..16 category DHT shape for
12-bit and 16-bit lossless JPEG, applies P-bit modulo difference wrapping,
opens/renders through `DicomImage.RenderImage()`, and decodes back to the source
frame byte-for-byte.

The remaining 412-byte `.dcm` file-size difference is outside the JPEG codestream
payload: the encapsulated JPEG frame size now matches the `fo-dicom.Codecs`
reference exactly.

Regression coverage:

- `ToolsCompressionPlanTests.CompressAll_jpeg_lossless_outputs_from_local_real_fixture_match_reference_frame_size_and_round_trip`
- `ToolsCompressionPlanTests.CompressAll_outputs_from_local_real_fixture_decode_and_render`

### JPEG-LS Lossless and Near-Lossless

Reference preserved from the `fo-dicom.Codecs` output in `D:\1_transcoded`:

- Reference JPEG-LS Lossless file size: 188,332 bytes.
- Reference JPEG-LS Lossless compressed frame size: 183,914 bytes.
- Reference JPEG-LS Near-Lossless file size: 77,696 bytes.
- Reference JPEG-LS Near-Lossless compressed frame size: 73,278 bytes.
- Reference JPEG-LS Near-Lossless SOS `NEAR`: 2.

Initial PureCodecs output:

- Pure JPEG-LS Lossless file size: 187,920 bytes.
- Pure JPEG-LS Lossless compressed frame size: 183,914 bytes.
- Pure JPEG-LS Near-Lossless file size: 56,425 bytes.
- Pure JPEG-LS Near-Lossless compressed frame size: 52,375 bytes.
- Pure JPEG-LS Near-Lossless SOS `NEAR`: 3.

Repaired PureCodecs output matches the reference compressed frame sizes for both
JPEG-LS transfer syntaxes. Lossless already produced the same codestream payload;
the 412-byte `.dcm` file-size difference is outside the JPEG-LS frame. Near-
lossless was smaller because the managed default `AllowedError` was 3 while the
`fo-dicom.Codecs` baseline uses `NEAR=2`. After changing the default to 2, the
remaining one-byte frame-size gap was DICOM encapsulation padding: the JPEG-LS
codestream ended on an odd byte count and needed a trailing `00` padding byte
after EOI. The tolerance assertion now compares 16-bit sample values instead of
individual bytes so signed 16-bit near-lossless output is checked against the
actual DICOM pixel semantics.

Regression coverage:

- `JpegLsCodecRoundTripTests.Default_parameters_match_fo_dicom_codecs_near_lossless_error`
- `JpegLsCodecRoundTripTests.Near_lossless_encode_pads_odd_length_jpeg_ls_frames_to_even_length`
- `JpegLsCodecRoundTripTests.Near_lossless_tolerance_assertion_compares_16_bit_sample_values`
- `ToolsCompressionPlanTests.CompressAll_jpegls_outputs_from_local_real_fixture_match_reference_frame_size_and_round_trip`
- `ToolsCompressionPlanTests.CompressAll_outputs_from_local_real_fixture_decode_and_render`

### HTJ2K Note

While collecting the format matrix, HTJ2K outputs from the same input failed
decode/render because the managed HTJ2K payload stored code-block dimensions as
16-bit values. Large 16-bit frames overflowed that field. The payload format now
uses a version 2 layout with 32-bit code-block dimensions while preserving
decode compatibility for existing version 1 payloads.
