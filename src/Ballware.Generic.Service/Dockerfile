FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY ./Ballware.Generic.Api ./Ballware.Generic.Api
COPY ./Ballware.Generic.Authorization ./Ballware.Generic.Authorization
COPY ./Ballware.Generic.Authorization.Jint ./Ballware.Generic.Authorization.Jint
COPY ./Ballware.Generic.Caching ./Ballware.Generic.Caching
COPY ./Ballware.Generic.Data ./Ballware.Generic.Data
COPY ./Ballware.Generic.Data.Ef ./Ballware.Generic.Data.Ef
COPY ./Ballware.Generic.Jobs ./Ballware.Generic.Jobs
COPY ./Ballware.Generic.Metadata ./Ballware.Generic.Metadata
COPY ./Ballware.Generic.Scripting ./Ballware.Generic.Scripting
COPY ./Ballware.Generic.Scripting.Jint ./Ballware.Generic.Scripting.Jint
COPY ./Ballware.Generic.Service ./Ballware.Generic.Service
COPY ./Ballware.Generic.Tenant.Data ./Ballware.Generic.Tenant.Data
COPY ./Ballware.Generic.Tenant.Data.SqlServer ./Ballware.Generic.Tenant.Data.SqlServer
COPY ./Ballware.Meta.Client ./Ballware.Meta.Client
COPY ./Ballware.Ml.Client ./Ballware.Ml.Client
COPY ./Ballware.Storage.Client ./Ballware.Storage.Client

WORKDIR "/src/Ballware.Generic.Service"
RUN dotnet build "Ballware.Generic.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ballware.Generic.Service.csproj" -c Release -o /app/publish

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Ballware.Generic.Service.dll"]