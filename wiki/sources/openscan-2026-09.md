---
title: "Source: OpenScan project pages and blog (retrieved 2026-09-11)"
type: source-summary
updated: 2026-09-11
sources:
  - https://openscan.eu/ (retrieved 2026-09-11)
  - https://openscan.eu/pages/openscan-benchy (retrieved 2026-09-11)
  - https://blog.openscan.eu/posts/openscan3-beta-whats-new/ (2026-04-17, retrieved 2026-09-11)
  - https://blog.openscan.eu/posts/openscan-macro-add-on-cost-of-open-source-hardware/ (2026-09-08, retrieved 2026-09-11)
  - https://blog.openscan.eu/posts/multivid-devlog-1/ (2026-06-02, retrieved 2026-09-11)
  - OpenScan3ODC/README.md, docs/ARCHITECTURE.md (local checkout, fork of OpenScan-org/OpenScan3)
---
Open-source photogrammetry turntable scanners, GPL-3.0 firmware, Germany-based, operating as a
business since 2019. Two devices (Mini, Classic) plus a Macro Add-on launched 2026-09-08 under a
"choose your price" experiment with a published cost breakdown. OpenScan Cloud processing is
optional and the firmware is explicitly hackable; local processing is supported.

**Vendor figures, recorded as claims and barred from `data/`.** The shop page states scan volumes of
~9 cm and ~18 cm cubes and accuracies "up to 0.02 mm" (Mini) and "up to 0.01 mm" (Classic), each
asterisked to the [OpenScan Benchy](https://openscan.eu/pages/openscan-benchy) page.

**The Benchy page was read (2026-09-11) and it is not a metrology benchmark.** It is a *qualitative
visual* comparison: a small model (originally by Valandar, printable from Printables) scanned by
various people on various devices, with results posted to Sketchfab and described in prose — "you
can see most layer lines and many print artifacts with great detail". There is **no ground-truth
geometry with stated dimensions, no deviation measurement, and no accuracy figure anywhere on the
page.** Its own stated purpose is so that users "do not have to fall for some marketing claims about
accuracy and resolution". Two further details: the Mini entry was processed through OpenScan Cloud,
and the Classic entry used a 21 MP Daheng MER2-2000-19U3C industrial camera reconstructed in Agisoft
Metashape — **not** the shipping IMX519 configuration, so even the qualitative comparison does not
represent a stock Classic. The page also compares photogrammetry software over one shared image set.

**Consequence:** the sub-0.02 mm figures have no readable methodology behind them. They are not
weakly sourced — they are unsourced, and the asterisk points at a page that exists to discourage
exactly the inference the figures invite. Per the grounding rule they may not enter a
`compare --declared-accuracy` argument or any `data/` entry. **Only a local measurement closes this**;
see [[open-questions]] item 18, closed as a refusal.

**OpenScan3 firmware (beta since 2026-04-17)** — the integration-relevant half, confirmed against the
local `OpenScan3ODC` checkout rather than the blog alone: Python FastAPI backend; **versioned API**
mounted at `/vX.Y` with a `/latest` alias and a `/versions` discovery endpoint, per-version OpenAPI;
centralised **background task system** with autodiscovery, persistence/restore and a documented
`tasks/community/` extension point (`docs/TASKS.md`); WebSockets; a system-update API kept behind a
narrow `sudo` wrapper; hardware abstraction split into stateful / switchable / event categories.
Capture is 2–3× faster than OpenScan2, on-device focus stacking landed, endstops and software angle
clamping exist, and a feature-quality overlay flags featureless surfaces before a scan. **An SDK is
stated as "what's next"** — the API already exposes granular control and they intend to wrap it.

**Multivid devlog** is a second signal about how this team builds: Raspberry Pi Zero camera nodes each
running a small FastAPI service, Ansible-provisioned for reproducibility, coordinated from a host,
with a deliberate **session / take / profile** metadata model chosen so footage is machine-readable
later. That is a provenance-shaped structure arrived at independently, and it maps onto run / inputs
/ parameters without translation.

**What it is to [[opendesigncore]]:** a candidate capture device for the half ODC's ROADMAP "Not ever"
refuses to own. See [[openscan]] for the assessment, including the scale problem that matters more
than the accuracy figure.
