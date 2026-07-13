# ARM64 Deployment and Hackathon Validation

SmartShelf AI is designed as an ARM64 edge AI system. The physical target is the Orange Pi 5 Plus, using its ARM64 CPU complex for the shelf control loop and its RK3588 NPU as the intended acceleration path for optimized edge inference.

## Local Container Demo

The default platform is `linux/amd64`, which is appropriate for the current Podman development machine.

```powershell
podman compose --profile simulator build
podman compose --profile simulator up -d
```

The services are available at:

- Dashboard: `http://localhost:5100`
- API: `http://localhost:5099`
- Simulator: optional `simulator` profile, posting to the API over the Compose network

Stop the demo without deleting SQLite data:

```powershell
podman compose --profile simulator down
```

## ARM64 Container Build

Select ARM64 for every service before building:

```powershell
$env:SMARTSHELF_PLATFORM = "linux/arm64"
podman compose --profile simulator build
```

On the Orange Pi, `linux/arm64` is native. On an x64 development machine, this build requires ARM64 emulation in the container engine and will be slower.

## ARM64 Publish Without Containers

```powershell
dotnet publish .\src\SmartShelf.Device.Simulator\SmartShelf.Device.Simulator.csproj -c Release -r linux-arm64 --self-contained false
```

## Configuration

The API stores SQLite data in the `smartshelf-data` named volume. Override these settings through environment variables when deploying:

- `ConnectionStrings__SmartShelf`
- `SmartShelfApi__BaseUrl`
- `SMARTSHELF_API_URL`
- `SMARTSHELF_PLATFORM`

## Demo Checklist

- API, Dashboard, and Simulator images build successfully.
- Simulator or device reports `processArchitecture` as `Arm64` on the board.
- Healthy, warning, critical, and offline scenarios reach SQLite and the Dashboard.
- Warning and critical decisions do not require a cloud round trip.
- Benchmarks compare ARM64 CPU inference with quantized RK3588 NPU inference.
