# Device Emulator

The ordered ARM64 device is expected to arrive in about two weeks. Until it is available, SmartShelf AI uses `SmartShelf.Device.Simulator` as the emulator for shelf telemetry, local edge decisions, and LED-state output.

## Purpose

The emulator keeps development aligned with the final physical device:

* It emits shelf telemetry in a deterministic format.
* It runs the same kind of local decision loop expected on the ARM64 controller.
* It produces green, yellow, red, and blue LED states for demo scenarios.
* It prints the process architecture so ARM64 validation can be shown clearly in logs.

## Run

```powershell
dotnet run --project .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -- --scenario demo --cycles 12
```

Available scenarios:

* `demo` - cycles through healthy, warning, critical, and offline states.
* `normal` - healthy stock and expiration window.
* `warning` - low stock or near-expiration risk.
* `critical` - expired product or critically low inventory.
* `offline` - sensor unavailable.

## ARM64 Validation

Publish the emulator for ARM64:

```powershell
dotnet publish .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -c Release -r linux-arm64 --self-contained false
```

Expected demo evidence:

* The emulator starts on an ARM64 runtime or ARM64 container.
* Output JSON includes `processArchitecture` as `Arm64` when running on real ARM64.
* Warning and critical scenarios produce yellow and red LED decisions locally.
* Offline mode produces a local fallback decision without a cloud dependency.

## Hardware Swap Plan

When the physical device arrives, keep the emulator as the regression test source and add a hardware adapter beside it. The real adapter should map sensor readings into the same telemetry shape used by the emulator so dashboard, AI, and alert logic do not need to change.

## Orange Pi 5 Plus Alignment

The emulator is shaped around the planned Orange Pi 5 Plus deployment. It validates the CPU-side control loop first, then leaves a clear path to move AI scoring onto the RK3588-class NPU once the board is available and the model/runtime path is selected.

The important contract is the emitted event shape: shelf reading, process architecture, local decision, LED color, and confidence. The real hardware adapter should preserve that contract.

## Pre-Hardware Development Scope

Before the device arrives, emulator work should focus on the telemetry and decision contract. Camera and wireless module integration should wait until the Orange Pi 5 Plus kit is available, but the emulator can already generate the events the camera or sensors will later confirm.
