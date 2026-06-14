FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TradeIntelligence.Api/TradeIntelligence.Api.csproj ./TradeIntelligence.Api/
RUN dotnet restore ./TradeIntelligence.Api/TradeIntelligence.Api.csproj

COPY src/TradeIntelligence.Api/ ./TradeIntelligence.Api/
RUN dotnet publish ./TradeIntelligence.Api/TradeIntelligence.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "TradeIntelligence.Api.dll"]
