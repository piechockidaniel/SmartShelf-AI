# SmartShelf AI – Intelligent IoT Shelving System for Inventory Monitoring and Retail Automation

## ARM64 Hackathon Focus

SmartShelf AI is designed for the Arm Create: AI Optimization Challenge as an edge-first AI and IoT system for ARM64 hardware. The target device is an Orange Pi 5 Plus with a Rockchip RK3588-class 8-core 64-bit processor, combining Cortex-A76 performance cores, Cortex-A55 efficiency cores, Mali-G610 graphics, and an embedded NPU rated up to 6 TOPS for mixed-precision edge AI workloads.

The project uses ARM64 advantages directly: local shelf decisions, low-latency LED feedback, offline-capable inference, compact deployment, and a realistic power profile for one controller per physical shelf. Cloud and dashboard components support visibility and integration, but the shelf controller should be able to classify healthy, warning, critical, and offline states locally.

Why ARM64 matters here:

* **Efficient always-on monitoring** - ARM64 is suitable for continuous inventory sensing without a server-class power draw.
* **Heterogeneous compute** - the Orange Pi 5 Plus provides CPU cores for control logic and an NPU path for future INT8/FP16 model acceleration.
* **Low-latency physical AI** - sensor readings can become green, yellow, or red LED decisions on the shelf instead of waiting for a cloud round trip.
* **Practical fleet scaling** - small ARM64 nodes make it realistic to deploy intelligence across many shelves.
* **Reproducible validation** - the simulator, services, and containers can be published for `linux-arm64` before the hardware arrives.

## Target Hardware

The planned device is **Orange Pi 5 Plus**:

* Rockchip RK3588-class 8-core 64-bit processor.
* Quad-core Cortex-A76 plus quad-core Cortex-A55 CPU layout, up to 2.4 GHz.
* Integrated Arm Mali-G610 GPU with OpenGL ES, OpenCL 2.2, and Vulkan 1.2 support.
* Embedded NPU with INT4, INT8, INT16, and FP16 mixed computing, up to 6 TOPS.
* 16 GB LPDDR4/LPDDR4x memory.
* eMMC module support and M.2 M-Key NVMe SSD expansion.
* Debian 11, Ubuntu 22.04, Android 12, and Orange Pi OS support.
* Dual 2.5G Ethernet expansion, HDMI input/output, USB 3.0, USB 2.0, and USB-C connectivity.

For the hackathon, the device will be used as the ARM64 edge controller for shelf telemetry, local AI scoring, and LED-state output.

The ordered kit includes the Orange Pi 5 Plus board, power supply, heat dissipation shell, Wi-Fi 6 wireless module, 32 GB card, and a 13 MP camera module for optional shelf-vision experiments.
## Project Overview

**SmartShelf AI** is an intelligent shelving platform that combines IoT sensing, embedded AI, and enterprise software integration to transform traditional inventory management into a proactive, real-time decision support system. The project addresses one of the most common challenges in retail, warehousing, pharmacies, and logistics: maintaining accurate inventory status while minimizing waste and simplifying operational workflows.

Each shelf continuously monitors the products placed on it and correlates collected information with external accounting or ERP software. Instead of relying solely on manual inventory checks, SmartShelf AI automatically evaluates product status based on configurable business rules and immediately communicates the result through intuitive visual indicators.

The system supports monitoring of multiple product attributes, including:

* Current inventory level
* Expiration or "sell-before" date
* Assigned storage location
* Product availability
* Stock replenishment status
* Custom business parameters synchronized from accounting software

A three-color LED indicator provides an immediate visual representation of the product or shelf condition:

* **Green** – Product status is normal and no action is required.
* **Yellow** – Attention is recommended, such as low stock, approaching expiration, or pending relocation.
* **Red** – Immediate action is required due to critical stock levels, expired products, inventory mismatch, or other high-priority events.

The LED status can represent either an individual product or the overall health of an entire shelf, allowing employees to identify issues instantly without consulting management software.

Beyond simple rule-based monitoring, SmartShelf AI incorporates embedded AI to recognize operational patterns and improve inventory management. AI models can analyze historical stock movement, predict replenishment needs, detect anomalies, prioritize alerts, and optimize shelf utilization. Running intelligence directly on edge devices reduces latency, minimizes cloud dependency, and enables reliable operation even with intermittent network connectivity.

The IoT architecture enables secure communication with accounting systems, warehouse management software, and cloud services. Product information is continuously synchronized, ensuring that the physical inventory accurately reflects digital records. This integration significantly reduces manual data entry, inventory errors, and product losses.

The platform is designed to be modular and scalable. Individual shelf modules can operate independently or as part of a larger distributed system covering entire warehouses or retail stores. This architecture makes SmartShelf AI suitable for applications such as supermarkets, pharmacies, manufacturing facilities, warehouses, distribution centers, and industrial storage environments.

The project combines several modern technologies into a single embedded solution:

* IoT connectivity for real-time monitoring
* Embedded AI for predictive inventory management
* Sensor-based product detection
* ERP and accounting software integration
* Edge computing for local decision making
* Visual LED guidance for operational efficiency

The long-term vision is to create an intelligent inventory platform that not only reports the current state of products but also predicts future inventory needs, reduces waste, minimizes human error, and improves operational efficiency. By combining AI, IoT, and enterprise software integration within an embedded platform, SmartShelf AI demonstrates how modern edge computing can enhance retail and logistics while remaining scalable, energy-efficient, and practical for real-world deployment.

## Emulator Before Hardware Arrival

The physical ARM64 device is expected to arrive in about two weeks. Until then, `SmartShelf.Device.Simulator` is the project emulator and the default validation target. It produces deterministic shelf telemetry and local edge decisions so the ARM64 workflow can be developed, tested, and demonstrated before sensors are connected.

Run the emulator locally:

```powershell
dotnet run --project .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -- --scenario demo --cycles 12
```

Run a specific shelf state:

```powershell
dotnet run --project .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -- --scenario warning
dotnet run --project .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -- --scenario critical
dotnet run --project .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -- --scenario offline
```

The emulator prints JSON events containing the shelf reading, ARM process architecture, local status decision, LED color, and confidence score. This gives the hackathon submission a repeatable ARM64 validation path while the hardware is unavailable.


## ARM64 Build and Validation

Publish the emulator for the Orange Pi 5 Plus or another ARM64 Linux environment:

```powershell
dotnet publish .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -c Release -r linux-arm64 --self-contained false
```

Build an ARM64 container image:

```powershell
docker buildx build --platform linux/arm64 -f .\src\SmartShelf.Device.Simulator\Dockerfile -t smartshelf-device-simulator:arm64 .
```

The expected Devpost evidence is a short run showing ARM64 process architecture, local edge decisions, LED colors, and warning/critical transitions without cloud dependency.

