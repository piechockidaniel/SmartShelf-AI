FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/SmartShelf.Domain/SmartShelf.Domain.csproj", "src/SmartShelf.Domain/"]
COPY ["src/SmartShelf.Application/SmartShelf.Application.csproj", "src/SmartShelf.Application/"]
COPY ["src/SmartShelf.Dashboard/SmartShelf.Dashboard.csproj", "src/SmartShelf.Dashboard/"]
RUN dotnet restore "src/SmartShelf.Dashboard/SmartShelf.Dashboard.csproj"

COPY . .
RUN dotnet publish "src/SmartShelf.Dashboard/SmartShelf.Dashboard.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
USER $APP_UID
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartShelf.Dashboard.dll"]
