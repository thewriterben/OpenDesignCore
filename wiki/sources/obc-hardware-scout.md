---
title: "Source: obc-hardware-scout reports (2026-06-29, 2026-07-06)"
type: source-summary
updated: 2026-08-15
sources: ["Oh-Ben-Claw/Knowledge Base/hardware-scout-2026-06-29.md", "Oh-Ben-Claw/Knowledge Base/hardware-scout-2026-07-06.md"]
---
Automated weekly registry-growth proposals (propose-only; ready-to-paste Rust; §4 needs-verification gate). Registry-relevant durable facts for [[openpartscore]]: capability tokens added — npu, edge_tpu, hailo, nn_accel, kpu, tensor_rt, ethernet, thread, zigbee, battery, vpu. Verified VID/PIDs: Coral USB 1a6e:089a→18d1:9302 post-init; OAK-D Lite 03e7:2485 (re-enumerates after DepthAI boot); Arduino Nano ESP32 2341:0070; Feather ESP32-S3 TFT 239a:811d. **Shared-ID convention:** all native-USB ESP32 (C3/S3/C6/H2/P4/C5) enumerate 303a:1001; CH340 1a86:7523; Jetson Orin Nano 0955:7020 collides with jetson-nano — registry disambiguates by name, not VID/PID (schema-relevant!). I2C collision: Grove Vision AI V2 and SCD41 both 0x62. Rule: 0x0000 placeholder IDs never merge; new token = VALID_CAPABILITIES + taxonomy doc + test. Trend: edge accelerators 4–13 TOPS → 40 TOPS INT4 GenAI-class (Hailo-10H AI HAT+2, ~$130, Jan 2026); ESP32-C5 first dual-band Wi-Fi 6 RISC-V MCU.
