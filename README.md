# ⚡ Setting Niran V2 - PvP Optimization Suite

A high-performance **C#** optimization tool specifically designed to reduce system latency, optimize network packet prioritization, and enhance frame responsiveness for FiveM and GTA V gameplay.

### Key Features
* 🌐 **Network Latency Tuning:** Enables QoS packet priority (DSCP 46), disables network throttling, and configures TCP Low Latency (`AckFreq 1`, `NoDelay 1`).
* ⏱️ **Timer & Input Tweaks:** Forces 0.5ms Timer Resolution, disables HPET/dynamic tick, and applies 1:1 raw mouse input configurations.
* 🚀 **System & Process Management:** Sets high priority for FiveM/GTA processes, toggles Cloudflare DNS (1.1.1.1), and clears game caches.
