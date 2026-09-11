# Raw sources: archived web retrievals

Immutable. **Never edit a file in this directory.** If a retrieval is wrong or superseded, add a new
one with a new date and say so in the wiki page that cites it.

This directory exists because `wiki/CLAUDE.md` requires raw sources to be immutable and cited by
repo-relative path, and the OpenScan / AI-CAD ingest of 2026-09-11 was the first whose sources were
web pages — mutable, unpinnable, and citable only by URL (wiki open question 16, closed 2026-09-11).

## What lives here, and what does not

**Archived:** pages that are *mutable* and *load-bearing* — a vendor page, a shop listing, a blog
post, a docs page. These can be edited or deleted by their owner at any time, and a wiki claim that
rests on one is unverifiable the moment that happens.

**Not archived, cited directly by identifier:** sources whose identifiers are permanent by design —
arXiv IDs (versioned and immutable), DOIs, and git commits or tags. Citing `arXiv:2505.06507` or a
commit SHA already pins the bytes; copying them here would add a second copy that can drift from the
first without adding any guarantee.

## What these files are, precisely

Each file is the **retrieval rendering** produced by the fetch tool — the page converted to text at
the moment it was read — not the original HTML bytes and not a screenshot. It is what was actually
read and reasoned over, which is the thing a later reader needs to check. Where the tool truncated a
page, the file says so **at the point of truncation**; a partial archive that looks complete would be
worse than no archive.

Every file opens with a header block giving the URL, the retrieval date, the tool, and completeness.

## Files

| File | Source | Retrieved | Complete |
|---|---|---|---|
| `openscan-benchy-2026-09-11.md` | https://openscan.eu/pages/openscan-benchy | 2026-09-11 | yes |
| `openscan-home-2026-09-11.md` | https://openscan.eu/ | 2026-09-11 | yes |
| `openscan3-beta-whats-new-2026-09-11.md` | https://blog.openscan.eu/posts/openscan3-beta-whats-new/ | 2026-09-11 | yes |
| `openscan-macro-add-on-2026-09-11.md` | https://blog.openscan.eu/posts/openscan-macro-add-on-cost-of-open-source-hardware/ | 2026-09-11 | **no — truncated** |
| `openscan-multivid-devlog-1-2026-09-11.md` | https://blog.openscan.eu/posts/multivid-devlog-1/ | 2026-09-11 | **no — truncated** |

The two truncated files are cited only for claims that fall inside the retrieved portion. Neither
carries a number that reaches `data/`.
