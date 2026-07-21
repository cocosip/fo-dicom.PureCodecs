# Codec Performance Benchmarks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reproducible managed-codec benchmarks with fixture validation so optimization starts from a correct baseline.

**Architecture:** A standalone `net10.0` BenchmarkDotNet executable owns its fixture catalog and `--verify` correctness mode. The xUnit project does not reference the benchmark project or BenchmarkDotNet. Benchmarks construct fresh target datasets inside each timed call, while setup validates fixture compatibility and supplies reusable source pixel data outside the measurement.

**Tech Stack:** .NET 10, BenchmarkDotNet, fo-dicom 5.2.6, xUnit v3.

---

### Task 1: Add the benchmark project and fixture catalog

**Files:**
- Create: `benchmarks/fo-dicom.PureCodecs.Benchmarks/fo-dicom.PureCodecs.Benchmarks.csproj`
- Create: `benchmarks/fo-dicom.PureCodecs.Benchmarks/BenchmarkFixtureCatalog.cs`
- Modify: `Directory.Packages.props`
- Modify: `fo-dicom.PureCodecs.slnx`

- [ ] Add `BenchmarkDotNet` as a centrally managed development dependency and create a `net10.0` executable that references all PureCodecs assemblies.
- [ ] Implement fixture metadata for the six initial transfer syntaxes and link the bundled DICOM fixture files as project content.
- [ ] Add the benchmark executable to the solution.

### Task 2: Validate benchmark fixtures before measurement

**Files:**
- Create: `benchmarks/fo-dicom.PureCodecs.Benchmarks/BenchmarkFixtureVerifier.cs`
- Modify: `benchmarks/fo-dicom.PureCodecs.Benchmarks/Program.cs`
- Modify: `benchmarks/fo-dicom.PureCodecs.Benchmarks/BenchmarkFixtureCatalog.cs`

- [ ] Add a `--verify` command that loads every configured bundled compressed fixture, verifies its expected transfer syntax, and decodes it through `PureTranscoderManager`.
- [ ] Keep the benchmark project out of the xUnit project-reference graph.
- [ ] Run `dotnet run --project benchmarks/fo-dicom.PureCodecs.Benchmarks -- --verify` before collecting results.

### Task 3: Add codec and transcoder benchmarks

**Files:**
- Create: `benchmarks/fo-dicom.PureCodecs.Benchmarks/CodecBenchmarks.cs`
- Create: `benchmarks/fo-dicom.PureCodecs.Benchmarks/Program.cs`
- Modify: `README.md`

- [ ] Add `[MemoryDiagnoser]` benchmarks for codec encode, codec decode, transcoder encode, and transcoder decode for every configured fixture.
- [ ] Set up DICOM services once per benchmark process and keep fixture validation and file I/O outside timed methods.
- [ ] Document the Release command, result location, and mandatory matching criteria for Go comparisons.

### Task 4: Verify and capture the initial managed baseline

**Files:**
- Modify: `docs/development/development-checklist.md`

- [ ] Run the benchmark fixture verifier.
- [ ] Run the full test suite.
- [ ] Run the benchmark executable in Release mode, exporting Markdown and JSON artifacts.
- [ ] Record the command and fixture matrix in the development checklist without committing generated machine-specific result files.
