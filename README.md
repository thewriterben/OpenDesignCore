# OpenDesignCore

Open engineering and design.

⟨One sentence: what this actually does.⟩

An open source computational engineering and design system for ⟨who⟩, built because ⟨what existing tools don't do⟩.

**Status:** pre-alpha. Nothing here is stable.

## Quick start

```
⟨install command⟩
⟨run the example⟩
```

Expected output: ⟨artifact and where it lands⟩. Should take under five minutes from a clean machine.

## What it is

- ⟨capability 1⟩
- ⟨capability 2⟩
- ⟨capability 3⟩

## What it is not

- ⟨explicit non-goal — this section saves more time than the one above⟩

## Conventions

Length is **millimetres**, matching the PicoGK kernel (ADR-0004); other quantities are SI unless dimensionally coupled to length, in which case the unit is stated explicitly rather than inferred. Conversion happens at exactly one place per boundary.

Resolution is **one global voxel size per model run** (ADR-0003), supplied explicitly by the caller, never defaulted in code, and recorded in the provenance of every artifact. A model below its resolution floor fails loudly rather than answering coarsely.

See [GLOSSARY.md](GLOSSARY.md) for terms, units, and sign conventions.

## Docs

| | |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Module boundaries and what crosses them |
| [GLOSSARY.md](GLOSSARY.md) | Domain terms, units, sign conventions |
| [DECISIONS.md](DECISIONS.md) | Why things are the way they are |
| [ROADMAP.md](ROADMAP.md) | Now / next / not yet |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Build, test, PR expectations |

## License

Apache-2.0 — see [LICENSE](LICENSE). (ADR-0005)
