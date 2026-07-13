# Orange Pi 5 Plus Target

SmartShelf AI targets the Orange Pi 5 Plus as the physical ARM64 shelf controller for the Arm hackathon build.

## Hardware Profile

* Rockchip RK3588-class 8-core 64-bit processor.
* Quad-core Cortex-A76 plus quad-core Cortex-A55 CPU layout, up to 2.4 GHz.
* 8 nm process design.
* Arm Mali-G610 GPU with OpenGL ES 1.1/2.0/3.2, OpenCL 2.2, and Vulkan 1.2 support.
* Embedded NPU supporting INT4, INT8, INT16, and FP16 mixed computing, up to 6 TOPS.
* 16 GB LPDDR4/LPDDR4x memory.
* eMMC socket with module options and M.2 M-Key NVMe SSD support.
* M.2 E-Key slot for Wi-Fi 6/Bluetooth modules.
* HDMI output, HDMI input, USB 3.0, USB 2.0, USB-C, and dual 2.5G Ethernet expansion.
* Supported operating systems include Orange Pi OS, Android 12, Debian 11, and Ubuntu 22.04.

## Role in SmartShelf AI

The Orange Pi 5 Plus is the edge controller mounted near the shelf. Its job is to keep the local decision loop running:

1. Read sensor or emulator telemetry.
2. Normalize product and shelf signals.
3. Run rule-based checks and AI scoring locally.
4. Drive LED guidance immediately.
5. Publish state to dashboard, MQTT, or ERP integration when available.

## ARM64 Advantages for the Submission

* Cortex-A76 cores provide enough CPU headroom for .NET services, telemetry processing, and dashboard-adjacent workloads.
* Cortex-A55 cores are useful for background monitoring and low-power continuous tasks.
* The NPU provides a concrete path for optimized INT8/FP16 edge inference after the baseline emulator is stable.
* Local decision-making avoids cloud latency for physical shelf feedback.
* The board's Ethernet, USB, NVMe, and wireless expansion options make it credible as a real retail or warehouse controller.

## Pre-Arrival Emulator Strategy

Until the board arrives, `SmartShelf.Device.Simulator` is the development target. It emits the same kind of shelf telemetry and local decision output expected from the real device. When hardware arrives, the sensor adapter should replace the emulator input while preserving the same event contract.

## Hackathon Device Kit

The ordered kit includes:

* Orange Pi 5 Plus board.
* Power supply.
* 13 MP camera module, listed as `1300W camera` in the kit description.
* Orange Pi wireless module R5 / Wi-Fi 6 module.
* Realtek RTL8852BE PCIe/USB wireless chipset reference from the module label.
* Heat dissipation shell.
* 32 GB card for operating system and project image.

## Planned Peripheral Roles

* **Camera** - optional visual shelf verification, product-presence checks, and demo footage for physical AI.
* **Wi-Fi 6 module** - wireless dashboard/MQTT connectivity when Ethernet is not available.
* **Dual Ethernet / 2.5G expansion** - stable wired networking for warehouse or retail deployments.
* **Heat dissipation shell** - supports sustained edge workloads and demo stability.
* **32 GB card** - initial OS image, logs, emulator output, and local model assets.
