# Dependencies

Every dependency is a liability someone else controls. Each one gets a row before it gets a commit.

## Kernel stack (ADR-0001 substance, ADR-0008 mechanics)

| Dependency | Pinned | License | How consumed |
|---|---|---|---|
| PicoGK (NuGet) | `[2.2.0]` exact (runtime 26.2, OpenVDB v13 bundled) | Apache-2.0 | `PackageReference` — runtime ships in the package; no separate install (ADR-0008) |
| leap71/LEAP71_ShapeKernel | tag `ShapeKernel-v2.1.0` (`313d676`) | Apache-2.0 | git submodule `external/LEAP71_ShapeKernel`, compiled as sources |
| leap71/LEAP71_LatticeLibrary | ⟨if a model needs it⟩ | Apache-2.0 | submodule, same pattern |
| leap71/LEAP71_QuasiCrystals | ⟨if a model needs it⟩ | Apache-2.0 | submodule, same pattern |

Upgrading any of these is a deliberate, tested change with its own commit. PicoGK 2.3.0 exists on NuGet and is not yet adopted.

Not used: `thewriterben/leap71ODC` is a fork of `leap71/leap71`, the organisation's landing-page repo — README and images only. It contains no kernel code and is not part of the build.

## Packages

| Package | Version | License | Why nothing already here will do |
|---|---|---|---|
| ⟨…⟩ | ⟨…⟩ | ⟨…⟩ | ⟨…⟩ |

## License compatibility

Project licence: Apache-2.0 (ADR-0005, accepted 2026-08-15).

The upstream stack is Apache-2.0, so the whole tree now carries one licence with one set of NOTICE obligations. Tools invoked as external processes (e.g. KiCad, GPLv3, via the electronics peer) do not affect this project's licence — their outputs are not derivative works, but keep invocation at arm's length (CLI/IPC), never linked. Verify the licence text of the specific version you pin rather than trusting a summary. If the choice has commercial consequences, ask a lawyer.

## Format policy

Prefer interchange formats that outlive the project: OpenVDB for fields, STL and 3MF for print, STEP where a downstream tool demands it, plain text everywhere else. No proprietary-only format in the core; adapters live at the edges.
