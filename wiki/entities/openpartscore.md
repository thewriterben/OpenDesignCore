---
title: OpenPartsCore
type: entity
updated: 2026-08-15
sources: [OpenPartsCore/README.md, OpenPartsCore/DECISIONS.md]
---
Canonical parts registry (PD-2/PD-3): JSON Schema v0 + one-part-per-file data under four namespaces (boards, electronic, mechanical, material), every entry cited, stdlib validator enforcing citations and the `_mm` unit-suffix rule. Boards namespace ingests [[oh-ben-claw]]'s registry.json (schema_version 1) with per-entry provenance — first entry: boards/esp32-s3. Bindings for Rust/TS/Python/C# will be generated, never hand-copied (codegen tool needs its own ADR). User inventory explicitly excluded. Apache-2.0. Repo: F:\Documents\GitHub\OpenPartsCore.
