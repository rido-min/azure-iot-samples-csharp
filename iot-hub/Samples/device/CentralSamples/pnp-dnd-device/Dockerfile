#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["CentralSamples/pnp-dnd-device/pnp-dnd-device.csproj", "CentralSamples/pnp-dnd-device/"]
RUN dotnet restore "CentralSamples/pnp-dnd-device/pnp-dnd-device.csproj"
COPY . .
WORKDIR "/src/CentralSamples/pnp-dnd-device"
RUN dotnet build "pnp-dnd-device.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "pnp-dnd-device.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "pnp-dnd-device.dll"]