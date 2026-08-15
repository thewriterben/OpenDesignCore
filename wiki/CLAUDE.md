# Wiki schema

This directory is the LLM Wiki layer from ADR-0006 (proposed). It follows the LLM Wiki pattern (Karpathy, April 2026): the agent writes and maintains every page here; the human curates sources and asks questions.

## Layers

- **Raw sources** — immutable. They live *outside* this directory: research PDFs and docs in ProjectBINGO, Oh-Ben-Claw `Knowledge Base/`, AdvancedStudio `docs/Research-Report.md`, OBC-Prime docs, datasheets, papers. Cite them by repo-relative path. Never edit a source.
- **Wiki** — this directory. Fully agent-owned. Rewrite pages freely as understanding improves.
- **Schema** — this file. Co-evolve it with the human.

## The grounding rule (ADR-0006)

No number in a wiki page may enter a model run. Values used by code come from `data/` with a citation. The wiki may read the ledger; it never writes to it. A wiki page never cites another wiki page as evidence — evidence citations point at raw sources or the ledger only. `[[links]]` between pages are navigation, not grounding.

## Structure

```
wiki/
  CLAUDE.md      this schema
  index.md       catalog of all pages, by category; updated on every ingest
  log.md         append-only; entries start "## [YYYY-MM-DD] <op> | <title>"
  entities/      one page per repo, tool, kernel, standard, vendor
  concepts/      cross-cutting syntheses: ecosystem map, use cases, open questions
  sources/       one short summary page per ingested raw source (points at the original)
```

## Page conventions

- YAML frontmatter: `title`, `type` (entity|concept|source-summary), `updated`, `sources` (list of raw-source paths).
- Wiki-links with `[[page-name]]`. Flag contradictions inline with `**Conflict:**`.
- Keep pages short and dense. A page that restates a source at length is a failed page; link and summarize.

## Operations

- **Ingest**: read source → discuss takeaways → write/update `sources/` summary → update touched `entities/` and `concepts/` pages → update `index.md` → append to `log.md`.
- **Query**: read `index.md` first, drill in, synthesize with citations. File durable answers back as concept pages.
- **Lint**: check for contradictions, stale claims, orphan pages, missing cross-refs. Append findings to `log.md`.
