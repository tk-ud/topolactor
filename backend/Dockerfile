FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ .
RUN dotnet publish Topolactor.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY docs/design/topology-layout-class-ssot.yaml /app/ssot/topology-layout-class-ssot.yaml
ENV TOPOLACTOR_TOPOLOGY_LAYOUT_CLASS_SSOT_PATH=/app/ssot/topology-layout-class-ssot.yaml
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "Topolactor.Host.dll"]
