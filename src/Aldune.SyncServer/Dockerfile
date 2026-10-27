FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Aldune.SyncServer/Aldune.SyncServer.csproj
RUN dotnet publish src/Aldune.SyncServer/Aldune.SyncServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# No root: con RCE o un fallo de rutas el atacante no sería root dentro del contenedor. /data se crea ya del usuario `app` para que un volumen
# NUEVO herede ese dueño; uno ya existente (creado como root) hay que pasarlo con `chown -R 1654:1654` antes de actualizar (ver docs/SYNC.md).
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Aldune.SyncServer.dll"]
