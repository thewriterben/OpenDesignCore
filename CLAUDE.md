# Working in this repo

OpenDesignCore. Read first, in this order: `ARCHITECTURE.md`, `GLOSSARY.md`, `DECISIONS.md`, then `ROADMAP.md` for scope.

C# on .NET, built on PicoGK and the LEAP 71 ShapeKernel, both pinned — but consumed differently, and the difference matters (ADR-0008): **PicoGK is a NuGet `PackageReference` at `[2.2.0]` with its runtime bundled** (no separate native install), while **ShapeKernel is a git submodule** at `external/LEAP71_ShapeKernel`, tag `ShapeKernel-v2.1.0`, compiled as sources. Geometry is voxel/SDF fields over OpenVDB — we do not write geometry algorithms that belong upstream.

## Verify with

```
dotnet build OpenDesignCore.sln -c Release
dotnet test OpenDesignCore.sln -c Release        # includes tests/reference/ — known answers, not pinned outputs
dotnet format OpenDesignCore.sln --verify-no-changes --exclude external
```

Toolchain: .NET SDK 10.x building net9.0; PicoGK pinned `[2.2.0]` from NuGet; ShapeKernel submodule at tag `ShapeKernel-v2.1.0` (ADR-0008). `git submodule update --init` after clone.

Run these after every step. Don't move forward while anything is red.

## Non-negotiables

- Length is millimetres, matching the kernel (ADR-0004). Other units are stated explicitly, never inferred. Conversion happens at one place per boundary.
- Voxel size is an explicit input to every model run, never defaulted in code, always recorded in provenance (ADR-0003). A model below its resolution floor fails loudly.
- Deterministic output: same inputs, same voxel size, same pinned versions produce byte-identical results.
- Submodules are pinned to tags. Upgrading one is its own commit.
- Validate geometry at boundaries before exporting or handing off to fabrication; fail loudly and specifically.
- Artifacts carry provenance: inputs, voxel size, pinned versions, tool version, commit. An artifact without one is not a result.
- Degradation is never silent — fallbacks and coarsened resolution appear in the returned result, not just a log.
- Never invent material properties, constants, or standards references. Cite or mark `TODO(source)`.

## How to work

- Open the actual files. Never infer an API from its name. For ShapeKernel that means reading the submodule source under `external/`. **PicoGK has no source in this tree** — it is a NuGet package (ADR-0008), so its behaviour is checked by decompiling the assembly or, better, by *measuring it*: write a test that compares the effect against its absence. This is not pedantry. On 2026-09-11 `mshFromStlFile` was found to accept a scale argument and silently ignore it, unnoticed for 27 days because every test used scale `1.0` — the one value where working and broken look identical. A dependency's signature is a claim, and a claim is not evidence. See `wiki/entities/picogk.md`.
- Anything touching more than one file gets a short plan first.
- Small steps, verified individually.
- Don't add code nothing calls. Documented-but-unreachable feature: wire it or delete it, and say which.
- Same error twice: stop and report rather than trying a third variation.
- Expensive-to-reverse choice: write the ADR in the same change.
