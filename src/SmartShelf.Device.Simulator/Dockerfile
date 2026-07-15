FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["src/SmartShelf.Device.Simulator/SmartShelf.Device.Simulator.csproj", "src/SmartShelf.Device.Simulator/"]
RUN dotnet restore "src/SmartShelf.Device.Simulator/SmartShelf.Device.Simulator.csproj"

COPY . .
RUN dotnet publish "src/SmartShelf.Device.Simulator/SmartShelf.Device.Simulator.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartShelf.Device.Simulator.dll"]
