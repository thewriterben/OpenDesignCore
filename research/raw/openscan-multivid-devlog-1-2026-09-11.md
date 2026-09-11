<!--
ARCHIVED RETRIEVAL — DO NOT EDIT
source:     https://blog.openscan.eu/posts/multivid-devlog-1/
published:  2026-06-02 (author: esto / Elias Stognienko)
retrieved:  2026-09-11
tool:       web_fetch (HTML → text rendering; not original bytes, not a screenshot)
complete:   NO — TRUNCATED BY THE RETRIEVAL TOOL. The truncation point is marked inline below.
            The article is an 8 min read; the retrieval stops inside the folder-structure code
            block, so anything after it was never read.
load-bearing for: the session/take/profile metadata observation and the Ansible-provisioned Pi
                  camera-node architecture in wiki/sources/openscan-2026-09.md. Both fall inside
                  the retrieved portion.
-->

# Multivid Devlog 1: Automating Multi-Angle Video for OpenScan

Can OpenScan document itself? A first devlog about building a Raspberry Pi multicam rig for automated scan videos.

Posted Jun 2, 2026 · By *esto (Elias Stognienko)* · 8 min read

OpenScan is easy to understand once you [see it in action](https://www.youtube.com/shorts/5lUPJOdkE0k). A scan session can produce a nice object, a satisfying machine movement, interesting camera angles, before/after comparisons, progress shots and plenty of material for tutorials or social media. But recording and arranging all of that by hand is tedious.

For a small open source / open hardware project, that is a real bottleneck. We need attention from potential users, contributors and customers, but we would rather spend our time building hardware, writing software and documenting what we learn than manually producing marketing videos all day.

So the idea behind `multivid` is simple: make OpenScan3 scan sessions easier to film, easier to reproduce and eventually easier to turn into finished video drafts **automatically**.[1]

That led us to experiment with small camera nodes that can be set up, started and stopped together, so that OpenScan scan sessions can be documented more reliably.

As Thomas went to Berlin to attend the [Open Hardware Summit](https://oshwa.org/events/open-hardware-summit-2026/), I seized the opportunity to repurpose the [prototype of the multicamera rig](https://blog.openscan.eu/posts/prototype-openscan-multi-camera-rig/).

The goal for our first milestone is straightforward: consistently record videos from different viewpoints at the same time with one shell command.

> **Support Note.** But first, before we look at the problem from different angles: if you too want to help us replace the awkward marketing hustle with slightly overengineered open hardware automation, you can support OpenScan by [sponsoring us on Patreon](https://www.patreon.com/OpenScan) or by sharing this post. It helps us keep prototypes public, document the useful bits, and show OpenScan from a few more perspectives.

## The Components

We already have camera nodes consisting of Raspberry Pi Zeros with Arducam imx519 cameras on a custom-made PCB.

The current prototype is intentionally simple.

The setup currently includes:

**Raspberry Pi camera nodes**

- a small native Python FastAPI HTTP service on each node
- systemd for running the node service
- Samba shares for accessing the recorded files

**A host PC**

- using Ansible for setting up fresh nodes reproducibly
- a coordinator Python script prepare/start/stop video recordings on all nodes simultaneously

## Why Ansible?

I got into Raspberry Pis with model 1. I can't count how many sd cards I have flashed and `sudo apt update && ...` I have typed. I can't stand this anymore (though with the new raspberry pi imager the process got way nicer to use nowadays!). Besides: If every camera node has to be configured by hand, the setup becomes fragile very quickly. Small differences between nodes can lead to confusing behavior later: different packages, different service files, different camera settings, different folder permissions.

In the beginning, I considered building on top of our [custom OpenScan3 pi-gen images](https://github.com/esto-openscan/OpenScan3-pi-gen), but I figured this would be the wrong approach here.

The idea is that we can start with mostly fresh Raspberry Pi OS Lite images, enable SSH, boot the devices, and then let Ansible turn them into known camera nodes. That includes installing packages and drivers, deploying our Python service, writing configuration files, setting up systemd, creating the recording directory, configuring Samba, and checking whether the node service is reachable.

The best part? When we need to change something in the setup, we do it on the Ansible files (they're called playbooks and roles) on our host and rerun the setup command!

## Sessions, takes and profiles

The important part is not just recording video. We also need the footage to be structured, with machine-readable metadata that makes later automation possible.

If the system knows which scan session is being recorded, which camera angle produced which file, which profile was used, and which take belongs to which attempt, the footage becomes much easier to process later. That is why this prototype is not only about cameras, but also about sessions, takes, profiles and metadata.

The prototype uses three important concepts:

A **session** is one recording context. For example, a specific scan, product demo, tutorial segment or test setup.

A **take** is one recording inside a session. A session can contain multiple takes.

A **profile** describes how a recording should be made. For example, resolution, framerate, bitrate, shutter time, gain, white balance gains or focus behavior.

The resulting folder structure on a camera node looks roughly like this:

```
(code block returned as bare line numbers 1-12 only; contents not rendered)
```

<!-- ============================================================
     >>> RETRIEVAL TRUNCATED HERE <<<
     The folder-structure listing came back as line numbers without content, and
     everything after it was not returned. Do not cite this file for the on-node
     directory layout or for anything later in the article.
     ============================================================ -->
