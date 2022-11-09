FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine AS base
RUN apk add --update --no-cache ffmpeg icu-libs
RUN apk add libgdiplus --no-cache --repository http://dl-3.alpinelinux.org/alpine/edge/testing/ --allow-untrusted
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS publish
WORKDIR /src
COPY . .
WORKDIR "/src/Weekenders.TelegramTools.SyncProcessor"
RUN dotnet publish "Weekenders.TelegramTools.SyncProcessor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
VOLUME ["/session"]
ENTRYPOINT ["dotnet", "Weekenders.TelegramTools.SyncProcessor.dll"]
