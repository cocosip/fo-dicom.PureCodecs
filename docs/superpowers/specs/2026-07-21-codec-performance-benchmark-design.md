# Codec Performance Benchmark Design

## Goal

Establish a reproducible performance baseline for the managed codecs before
optimizing them, while preserving the repository's existing compatibility and
pixel-correctness guarantees.

## Scope

Add a standalone `net10.0` BenchmarkDotNet executable under `benchmarks/`.
It will benchmark both of these boundaries for representative, bundled DICOM
fixtures:

- Codec boundary: `IDicomCodec.Encode` and `IDicomCodec.Decode`.
- Application boundary: `DicomTranscoder.Transcode` from uncompressed DICOM to
  the target transfer syntax and back to Explicit VR Little Endian.

The initial fixture matrix covers RLE Lossless, JPEG Baseline Process 1, JPEG
Lossless Process 14 SV1, JPEG-LS Lossless, JPEG 2000 Lossless, and JPEG 2000
Lossy. Each benchmark uses the existing 960x540 acceptance fixtures where one
exists. Encode uses the bundled uncompressed RGB acceptance fixture; decode
uses the bundled compressed fixture for the matching transfer syntax.

HTJ2K is intentionally deferred from the initial matrix because its current
fixtures and independent Native validation are process-isolated. It will be
added only after the ordinary benchmark infrastructure is established and the
fixture is proven representative.

## Measurements

Every benchmark run must report:

- Mean and distribution of elapsed time.
- Managed allocation per operation and garbage collection counts.
- Fixture name, dimensions, frame count, transfer syntax, operation, and codec
  implementation in the exported result.

The benchmark does not use performance assertions. Machine speed, CPU state,
and runtime version make such assertions unsuitable for CI. Correctness checks
remain normal xUnit tests and run before comparing benchmark results.

## Correctness Gates

Before collecting or accepting a baseline:

1. The full `fo-dicom.PureCodecs.Tests` suite must pass.
2. A benchmark fixture validation test must verify that every configured file
   exists, has the expected transfer syntax, and can be decoded by the managed
   codec.
3. Lossless outputs must preserve pixels exactly; JPEG Baseline and JPEG 2000
   Lossy outputs must use the existing measured tolerance assertions.
4. Benchmark setup validates the input once but excludes validation from the
   timed operations.

## Comparison Rules

The C# benchmark measures managed PureCodecs only. Go comparisons are made by
running an equivalent `go test -bench` suite in `go-dicom-codec`, using the
same source pixels or compressed DICOM frames, transfer syntax, codec settings,
and operation boundary. Results must identify runtime, CPU, operating system,
and fixture; a number from a different fixture, quality, or boundary is not a
valid comparison.

The native `fo-dicom.Codecs` implementation may be profiled separately as a
diagnostic reference, but it is not a target implementation and is not added
to the initial benchmark executable.

## Optimization Process

Use the initial result to select one hot path. For each optimization:

1. Add or retain a focused regression test for the affected codec behavior.
2. Verify the test fails before the behavioral fix when applicable.
3. Implement the smallest allocation or algorithmic change.
4. Run the focused correctness test and the full test suite.
5. Re-run the exact same benchmark job and retain before/after result files.

Production assemblies remain pure C#, target `netstandard2.0`, and must not
add native dependencies, P/Invoke, or a native fallback.
