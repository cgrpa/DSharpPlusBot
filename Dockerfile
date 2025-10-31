# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# ---------- Base image: runtime ----------
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime:9.0 AS base

# Install runtime dependencies (for Opus + Sodium)
RUN apt-get update && apt-get install -y \
    libopus0 \
    libopus-dev \
    libsodium23 \
    libsodium-dev \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
WORKDIR /app


# ---------- Build image ----------
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

# Install dev headers (needed if native libs compile)
RUN apt-get update && apt-get install -y \
    libopus-dev \
    libsodium-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY ["TheSexy6BotWorker/TheSexy6BotWorker.csproj", "TheSexy6BotWorker/"]
RUN dotnet restore "./TheSexy6BotWorker/TheSexy6BotWorker.csproj"
COPY . .
WORKDIR "/src/TheSexy6BotWorker"
RUN dotnet build "./TheSexy6BotWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build


# ---------- Publish image ----------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TheSexy6BotWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


# ---------- Final image ----------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TheSexy6BotWorker.dll"]
