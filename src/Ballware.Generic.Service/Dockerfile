FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

WORKDIR "/src/Ballware.Generic.Service"
RUN dotnet build "Ballware.Generic.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ballware.Generic.Service.csproj" -c Release -o /app/publish

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Ballware.Generic.Service.dll"]