FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/SmartShelf.Domain/SmartShelf.Domain.csproj", "src/SmartShelf.Domain/"]
COPY ["src/SmartShelf.Application/SmartShelf.Application.csproj", "src/SmartShelf.Application/"]
COPY ["src/SmartShelf.Infrastructure/SmartShelf.Infrastructure.csproj", "src/SmartShelf.Infrastructure/"]
COPY ["src/SmartShelf.Api/SmartShelf.Api.csproj", "src/SmartShelf.Api/"]
RUN dotnet restore "src/SmartShelf.Api/SmartShelf.Api.csproj"

COPY . .
RUN dotnet publish "src/SmartShelf.Api/SmartShelf.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartShelf.Api.dll"]
