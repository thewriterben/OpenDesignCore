---
title: "Source: Creating MakerSpaces (Mannickarottu, Patterson, Godon; Apress 2025)"
type: source-summary
updated: 2026-09-25
sources:
  - doi:10.1007/979-8-8688-1309-2 (ISBN 979-8-8688-1309-2; permanent identifier, not archived — see wiki/CLAUDE.md "Web sources")
---
*Creating MakerSpaces: For Electronics, Arts, Engineering, and More.* Maker Innovations Series,
Apress, 2025. Eight chapters, ~72k words, read in full 2026-09-25. The authors built and run the
George H. Stephenson Foundation Educational Laboratory & Bio-MakerSpace at Penn Engineering, and
most worked examples are that space. **Explicitly US-centric** — the authors say so in chapter 2.

**What it is.** An operator's handbook, not a study: types of space (a user-cost × equipment-
specialisation matrix), the four maker fields and what each needs physically, equipment selection,
layout and utilities, the website as infrastructure, and — the part this platform cares about —
chapter 6 on operations: funding models, eight staff roles, a staff-training ladder, four
user-training methods matched to injury risk, training expiry, enforcement (physical, digital,
social), six policy areas, hours models, reservation tools, SOP families, maintenance cadences,
inventory discipline, and a risk table. Chapter 7 is community and marketing.

**The claim that matters here.** The book states that the hard problem in running shared machines
is *tying a reservation to the physical tool*: badge and power-interlock systems exist but cost
money, so most spaces run on trust, staff check-ins and defined consequences. It also describes the
authors' own Raspberry-Pi automations — an ID-card tap checked against a trained-user list with
three states (approved / not approved / blacklisted pending review), the same list tracking
borrowed equipment, and Google-Form submissions watched by a Pi that sends confirmations and
calendar events for 3D prints and supply orders. These are described at concept level only: **no
code, no schema, no procedure text** is in the book. Synthesised in [[makerspace-operations]].

**Numbers in it, all barred from `data/`.** The book gives very few figures and none are material
or process properties: a digital knitting machine "just under $16,000 as of December 2023", a USB
test instrument "less than $400", a desktop CNC router "under $1,000", and a user survey at Penn in
which "over 75 % of respondents" said the lab website made them more independent. Vendor and
survey claims, wiki-layer only.

**What it lacks, stated so nobody assumes otherwise.** No provenance concept — a part made in a
makerspace has no record of what made it. No treatment of machine calibration beyond "schedule
professional servicing". Reservation software is compared by feature list (Google Calendar,
Clustermarket, LibCal, NIST's open-source NEMO, MIT Mobius) from the authors' description, not
from reading the tools; none of those claims was verified in this ingest. Physical-computing
content is introductory. Treat every equipment recommendation as a 2023–2024 snapshot.

**Where it sits.** Read in full from the epub; all statements above are the book's, paraphrased.
Evidence quality: a single practitioner-authored trade book, one institution's experience
generalised. Good for the shape of an operating model; not a source for any value.
