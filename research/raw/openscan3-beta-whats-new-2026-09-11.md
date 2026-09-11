<!--
ARCHIVED RETRIEVAL — DO NOT EDIT
source:     https://blog.openscan.eu/posts/openscan3-beta-whats-new/
published:  2026-04-17 (author: Thomas Megel)
retrieved:  2026-09-11
tool:       web_fetch (HTML → text rendering; not original bytes, not a screenshot)
complete:   yes
load-bearing for: the OpenScan3 firmware description in wiki/sources/openscan-2026-09.md and the
                  integration shape in wiki/entities/openscan.md. Firmware structure claims were
                  cross-checked against the local OpenScan3ODC checkout, not taken from this post
                  alone.
note:       several outbound links in the original were Shopify click-tracking redirects
            (openscan.eu/_t/c/v3/...); they are preserved verbatim below as retrieved.
-->

# OpenScan3 Beta — What's New

Posted Apr 17, 2026 · By *Thomas Megel* · 4 min read

We just announced that OpenScan3 is moving into beta. [That post covers what beta means](https://openscan.eu/blogs/news/openscan3-reaches-beta) and where we still need help. This one is about what's actually in it. Elias has been working on the new firmware for over a year, and it's finally ready for broader use. Here's what changed.

## What does the new firmware mean for you:

### 2-3 times faster capturing

It is now significantly faster to capture a dataset thanks to improved handling of the camera and the motion controller. Capture times with the default IMX519 vary between 0.7s for manual focus and 1.2s for autofocus of full-res images (old software: 3-6s). The 64MP Hawkeye camera takes 5 - 12s per full-res image. This means that the new firmware runs at similar speeds to OpenScan2 at four-times higher pixel count!

### QR Code Wifi Setup

Setting up WiFi on a Raspberry Pi has always been a pain. We implemented a relatively simple solution for the user, so that you can just share the WiFi credentials directly from your smartphone by showing the scanner the generated QR code (which usually is hidden somewhere in your smartphone settings).

### Setup wizard & custom presets

On first boot, you can simply select the type of scanner and electronics. OpenScan3 now supports the community-built blackshield out of the box, alongside the Mini v2 and Midi.

### Polished UI & Extensive tooltips

The much more modern and responsive interface now includes tooltips throughout, explaining the many options available.

### Photogrammetry Feature preview

You can now get live feedback on the surface quality of your scan object. Toggle the overlay and "good" areas appear red while featureless areas appear blue (see below). This is especially useful for beginners getting a feel for which surfaces are easy to scan and which ones will need matting spray or a different approach.

### Histogram

A standard photography tool is now built into the firmware. The histogram gives you quick, intuitive feedback for setting shutter speed and avoiding over- or underexposed images.

### Arducam Hawkeye (64MP) camera

It is finally possible to use the 64MP Arducam Hawkeye. The new firmware fully supports this higher-resolution camera, which notably improves the scan quality (see OpenScan Benchy).

The camera is available in our shop. Note, that you'll need to select the right Raspbian Image for your SD card. Currently we need a separate image for the Hawkeye camera.

### Endstops & Angle Clamping

Motors can now be constrained to defined bounds, either via physical endstops or software clamping. Endstop support is currently implemented for the blackshield; we plan to wire up the endstop connectors on the default pre-soldered Pi shield soon.

### Seamless OpenScan Cloud integration

First, the important part: OpenScan Cloud is and will always be optional. The firmware is open source and it will always be an open and hackable system.

That said, we've made the cloud pipeline much easier to use. Activate the Cloud, enter your token in the settings and you can upload scans and download the finished model directly on the device, which could all be automated..

### On-device focus stacking

Close-up shots often suffer from a very narrow depth of field. Only a narrow slice of the object is sharp, and everything in front of or behind it blurs out. Photogrammetry struggles with those blurry areas and 3D scans might fail or contain a lot of artifacts. Taking images at varying lens positions and combining these photos with focus-stacking can solve this issue. OpenScan3 now does this focus stacking directly on the Raspberry Pi, which is a significant boost to scan quality.

## What's next

### Software Development Kit (SDK)

At this point, the new API already offers granular control over the device and exposes most of its functionality to the tech-savvy user. We're wrapping those functions into an SDK so you can build end-to-end workflows: control the scanner, capture images, post-process, run photogrammetry.

### External camera trigger & DSLR support

A wider range of cameras opens up real quality gains for 3D scans and other applications. nThe new modular structure makes adding new cameras much more straightforward.

*(sic — "nThe" appears in the original)*

### 3D Viewer and simple mesh tool

We're building a simple in-browser tool for mesh simplification and color correction, so you can turn raw scans into game-ready assets without leaving the browser.

**For developers, companies and researchers**

OpenScan3 is a solid foundation for any project that needs precise motion control, one or more cameras, and configurable I/O. That includes motorized camera sliders, microscopy stages, product photography rigs, stop-motion setups, and plenty more we haven't thought of yet.

Both software and hardware are easily customizable. If you are interested in the technical details of the software, check out the OpenScan3 code on Github and our recent blog posts.

And if you need a custom solution, feel free to reach out at any time!

**Getting started:**

Grab a new microSD card and follow the setup guide in this blog article. The firmware is in beta, so expect some minor changes. You may need to reflash at some point. That said, the current state is already a significant upgrade for almost all users. If you're not sure, if the update is right for you, check out this blog article about the meaning of the beta state and feel free to join the [discussion on Discord](https://discord.com/invite/gpaKWPpWtG).

Categories: Firmware · Tags: openscan3, beta, whats-new, firmware

This post is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) by the author.

*(sidebar "Recently Updated": OpenScan3 Firmware: How to run it from your fork · OpenScan Macro Add-on: Launch & The Cost of Open-Source Hardware · OpenScan3 Firmware: The New Updater · Multivid Devlog 2: Three Camera Nodes and a first Video · OpenScan3 Firmware: Example Tasks)*

*(sidebar "Further Reading": Apr 16 2026 OpenScan3 Firmware: Quick Start & Troubleshooting · Apr 16 2026 OpenScan3 Beta · Mar 6 2026 OpenScan3 Firmware: Smooth Moves — "replaces OpenScan2's 'delay tuning' with a simple motion model: max_speed and acceleration")*
