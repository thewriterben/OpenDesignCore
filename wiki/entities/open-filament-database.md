---
title: Open Filament Database
type: entity
updated: 2026-08-21
sources: [https://github.com/OpenFilamentCollective/open-filament-database, awesome-3d-printingODC/readme.md]
---
Community catalogue of 3D-printing filaments, hosted by the Open Filament Collective and facilitated by SimplyPrint. MIT, data free to redistribute and embed. Structure is brands → materials → product lines → colour variants → spool sizes, plus the stores that sell them. Published as a static REST API (`api.openfilamentdatabase.org`) with bulk JSON/NDJSON/SQLite/CSV downloads, addressed by path (`/api/v1/brands/{b}/materials/{M}/filaments/{f}/variants/{v}.json`) with UUIDs retained for integrations needing opaque ids. Dated dataset releases (latest seen: `dataset-v2026.07.10`). Related: the OpenPrintTag NFC spec consumes its UUIDs; SimplyPrint's `slicer-profiles-db` maps its entries to slicer profiles.

**What it is to [[opendesigncore]]:** an identity source, adopted as `FilamentRef` in ADR-0013. `--material pla` was a label two brands share, so a compensation measured on one spool was eligible for another; this names the variant. References are recorded as opaque pinned strings — never dereferenced at run time, because a network call inside a deterministic run makes a recorded result depend on a remote service's current contents.

**Conflict:** the `awesome-3d-printingODC` list describes it as a database of "print settings". It is not. It carries no shrinkage figures, no dimensional tolerances, no mechanical properties. Whoever reads that description and reaches for it as a data source will produce an invented material property with a citation attached. ADR-0013 states the rule before the mechanism for this reason, and the `TODO(source)` on `pla-generic.json` stays open — only a vendor TDS or a measurement closes it.

**Where it joins the platform:** [[advancedstudio]] tracks materials via CFS/RFID, and OpenPrintTag — which consumes these UUIDs — is the NFC format that sits underneath that. A spool identified in a comparison record and a spool read off a tag at the printer can in principle be the same identifier. Not wired; noted because it is the obvious next seam if spool identity ever needs to be automatic rather than typed.
