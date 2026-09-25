---
title: Makerspace operations as a platform concern
type: concept
updated: 2026-09-25
sources:
  - doi:10.1007/979-8-8688-1309-2 (Creating MakerSpaces, Apress 2025 — chapters 5–6; see [[creating-makerspaces-2025]])
  - DECISIONS.md ADR-0006, ADR-0007, ADR-0009, ADR-0011, ADR-0012, ADR-0013
  - src/OpenDesignCore/Provenance/Ledger.cs (runs and handoffs tables), src/OpenDesignCore/Runs/StudioHandoff.cs
  - ROADMAP.md "Not ever"
---

# Makerspace operations as a platform concern

**Standing.** Design-level. This engine does not own facility operations and must not (ADR-0007:
an engine among peers; ROADMAP "Not ever" refuses anything off the requirements → geometry →
provenance path). What this page does is map a practitioner's operating model for shared machines
onto contracts the platform already has, name the gaps, and put procedure *designs* on record for
whichever peer takes them. Claims about ODC cite its own ADRs and source; claims about peers are
taken from their entity pages ([[advancedstudio]], [[openbuildcore]], [[project-bingo]]) whose
sources were not re-read for this ingest, and say so where it matters.

## Why a makerspace is this platform's problem at all

A makerspace is, in ecosystem terms, a fabrication node with people in it — a [[project-bingo]]
node whose "human labor" node type is the majority of its capacity. The book's chapter 6 problem
statement is: *nothing stops a person taking a reserved tool or keeping it longer; power interlocks
cost money; so most spaces run on trust, staff check-ins, and consequences.* The platform has
already solved that for exactly one machine class, by a different route: a print does not start
unless a **proposal carrying the design's artifact hash** is approved by a human at the machine
(ADR-0009; studio `/api/propose`), and the decision — including a rejection — is a ledger row
(studio ADR-0003, per its entity page). That is the book's "proxy access" posture made
mechanical, with a provenance record the book never imagines. The question this page asks is what
it takes to extend that from "one printer with a human approver" to "a room of tools with
certified users", and which peer each piece belongs to.

## The mapping

| Book's operating concept (ch. 6) | Platform contract | Status | Owner |
|---|---|---|---|
| Proxy access — staff run the machine; user submits | Reads execute, writes propose; human approves at the machine (ADR-0009) | exists, one machine class | AdvancedStudio |
| "Tie the reservation to the physical tool" | Guarded proposal + append-only jobs ledger; ODC `handoffs` row (`run_id`, `artifact_sha256`, `destination`, `status` ∈ staged / staged-offline / proposed, `proposal_id`) | exists for printers; **gap** for lasers, CNC, woodshop tools | AdvancedStudio for its printer; unowned for the rest |
| Trained-user list: approved / not approved / **blacklisted pending review**; training method matched to injury risk; expiry and renewal | Nothing. Approval is per proposal by a human; the proposer's certification state is not a record anywhere | **gap — no peer owns people or certification** | unowned |
| Enforcement: floor tape and gates that never block egress, badge → machine enable, mentor pairing | Studio auth is a shared token on state-changing endpoints (per entity page); no per-person identity | gap | unowned |
| Maintenance cadence per machine (each use / weekly / monthly / quarterly), warranties and professional servicing dates | `axis_calibration` per axis with date, residual, method; Unknown / Partial / Verified, and a proposal refused unless all three axes verified (ADR-0012) | exists for **calibration only**; cadence tasks and servicing dates absent | OpenBuildCore (`machines.json`) |
| Consequence of neglect: a warranty does not cover chronic neglect | ADR-0012's reasoning is the same shape — a fault filed as a property | principle shared, record absent | OpenBuildCore |
| Master inventory sheet: per-item restock cadence, buffer stock, spot checks | Inventory is `part_id` + `qty` against OpenPartsCore; filament by spool identity, never value (ADR-0013) | partial — quantities exist, cadence and buffer do not | OpenBuildCore |
| Reservation software (Google Calendar, Clustermarket, LibCal, NEMO, MIT Mobius) | none in the platform | gap; NEMO (NIST, open source, certifications + maintenance + billing per the book — **unverified**, not read) is the adopt-before-build candidate | unowned |
| Revoked access as a consequence | Ledger records rejections | exists for proposals, not for people | — |
| Provenance of what was made | The platform's whole contract; the book has none | ODC supplies it; nothing on the book's side consumes it | ODC |

The single largest finding: **no peer owns the person.** Machines (OpenBuildCore), parts
(OpenPartsCore), designs and runs (ODC), proposals and approvals (AdvancedStudio), settlement
(BINGO) all have a home; *who is certified on what, until when, validated how* has none. Every
enforcement mechanism in the book hangs off that record. Filed as open question 22.

## Procedure designs (generic; variants below)

These are designs, not implementations. Field names are proposals for the owning peer's schema
and follow its conventions where one exists (dates, residuals and methods as in `axis_calibration`).

### 1. Certification: a state per (person, tool)

```
untrained ──train(method, validator, date)──▶ certified ──expires_on passed──▶ expired
   ▲                                              │                               │
   │                                     incident / staff flag                    │ renew (refresher or re-test)
   └──────── revoked ◀── review ◀── suspended ◀────┘                               ▼
                                     (book: "blacklisted pending review")      certified
```

Record: `person_id`, `tool_id`, `method` ∈ { self-paced-quiz | staff-guided | on-demand-quiz |
hybrid }, `validated_by`, `validated_on`, `expires_on` (null = does not expire), `validation_artifact`
(optional hash — the book's laser test is *design and cut a press-fit box and prove it fits*; a photo
or the box's own design hash is the artifact), `state`.

Rules the book states and the record should refuse to violate: a tool that can injure may not be
certified by `self-paced-quiz` alone (the method must include an in-person component); certification
from another facility does not transfer (protocols differ — the record is per facility); expiry is
per tool, six months to a year for hazardous tools, never for low-risk ones unless the tool changes.
Three states are deliberately not two, for the same reason ADR-0012 keeps Unknown apart from
Verified: *never certified* and *suspended pending review* look identical at the door and are
different facts.

### 2. Reservation → check-in → proposal → ledger

1. **Reserve** `(person, tool, slot)`. The reservation surface reads the certification record and
   refuses if `state ≠ certified` — refusal, not silent omission, naming the missing step.
2. **Check-in** at the tool: ID tap → certification current? → either **enable** (interlock; the
   book's badge-to-machine) or **staff proxy** (staff enable after a glance at the same record).
3. **Job proposal** carrying the design's artifact hash where one exists (ODC sidecar via
   `handoff`), plus `person_id` and `reservation_id`. Human approves at the machine (ADR-0009).
4. **Ledger row** for every outcome: approved, rejected, and **expired-unanswered** — the studio's
   TTL expiry currently leaves no row (observation on [[advancedstudio]], 2026-08-23), and a
   reservation system inherits that hole unless it writes its own.
5. **Use counter** on the machine increments, which is what makes cadence 3 below computable.

The interlock is optional; the record is not. A space that cannot afford power control still gets
a refusal at reservation time and a ledger of who was approved to run what — which is the part the
book says trust-based spaces lack.

### 3. Machine cadence record (extends `axis_calibration`)

Per machine, alongside the existing calibration: a list of `tasks` each with `cadence` (`per_use`
| `uses:N` | `days:N`), `last_done` (date, `by`), and a derived `overdue` flag; plus
`servicing_due` and `warranty_expires` as dates with a source. The book's 3D-printer example
(clean bed and check nozzle each use; level bed, clean nozzle, lubricate rails weekly; belts, fan,
firmware monthly; deep clean and worn-part replacement quarterly) is the seed set, **for a
practitioner to confirm against the manual of the actual machine** — the book itself says the
service details are at the back of each manual.

Policy is the owner's, but the state must be visible: a reservation surface shows `overdue`, and
an ADR-0012-style gate ("refuse to propose on a machine whose per-use tasks are overdue") is a
one-line rule once the record exists. Whether to gate is not decided here.

### 4. Consumables

Per item: `restock_cadence`, `buffer_qty` held elsewhere, `last_checked` (date, `by`), and a
senior-staff `spot_check` log. Sits naturally on OpenBuildCore's inventory (`part_id` + `qty`),
which today answers *what do we have* and not *when did anyone look*. The book's discipline —
"no user should ever find something empty" — is a cadence, not a quantity.

### 5. Minimum automation, in the book's own pattern

The authors' systems are a Raspberry Pi watching a Google Sheet fed by Google Forms, sending
email and calendar events, and an ID-tap script checking a list. The platform equivalent is an
MCP surface where reads execute and writes propose — same posture, with a ledger under it. The
order to build in, cheapest first: the certification record (a sheet is enough) → reservation
refusal on that record → ledger of approvals → use counters → cadence flags → interlock last.

## Variants by facility type

| | University teaching lab → open lab | Community / fab space (membership) | Public library | K-12 |
|---|---|---|---|---|
| Who validates | Two staff + student workers as certifiers, validated by mock-training a colleague | Paid staff, expert members | Library staff; mostly self-paced | The teacher |
| Identity | Institutional ID card; semester rhythm resets cohorts | Membership tier drives tool access; 24/7 access needs RFID + restricted tools during unstaffed hours | Library card; walk-ins | Class roster |
| Risk mix | Highest: lasers, wet lab (BSL), soldering, printers | Woodshop, CNC, lasers — injury-capable, in-person required | Mostly low-risk → quizzes suffice; printing by staff proxy | Kits; proxy for anything sharp or hot |
| Reservation pressure | Deadlines spike demand — caps and enforcement matter most | Revenue-linked; monopolisation is a member-relations issue | Low; first-come or staff queue | Class-scheduled |
| Where the record could live | Institution already has identity; certification record is the missing table | NEMO-class tool or a sheet | LibCal-class tool (per the book, unverified) | The teacher's roster |
| IP | IP-neutral is the draw (book: Strella, HueDx) — say it, and tell founders to consult counsel | Same, contractually | n/a | n/a |

## What ODC does and does not contribute

- **Does:** the design hash and provenance sidecar a proposal carries, so an approval is about a
  named design rather than a filename; `handoff` as the ledger row on this side; the refusal
  discipline (ADR-0009, ADR-0012) as the pattern the person-record should copy.
- **Does not, and must not:** gain an approval tool (ADR-0009's test forbids the name); own people
  or certification data; read any number from this page into a run (ADR-0006). The book's cadences
  and prices are wiki-layer and stay there.

## Open

Open question 22 in [[open-questions]]: which peer owns the certification record, or whether an
existing open-source tool (NEMO) is adopted for it — a decision that needs the tool read, not
the book's description of it.
