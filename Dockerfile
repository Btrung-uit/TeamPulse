# syntax=docker/dockerfile:1.7

FROM node:24-alpine AS frontend-build
WORKDIR /src/frontend

COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci

COPY src/frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-restore
WORKDIR /src

COPY global.json TeamPulse.sln ./
COPY src/backend/TeamPulse.Api/TeamPulse.Api.csproj src/backend/TeamPulse.Api/
COPY src/backend/TeamPulse.Application/TeamPulse.Application.csproj src/backend/TeamPulse.Application/
COPY src/backend/TeamPulse.Domain/TeamPulse.Domain.csproj src/backend/TeamPulse.Domain/
COPY src/backend/TeamPulse.Infrastructure/TeamPulse.Infrastructure.csproj src/backend/TeamPulse.Infrastructure/
RUN dotnet restore src/backend/TeamPulse.Api/TeamPulse.Api.csproj

COPY src/backend/ src/backend/

FROM backend-restore AS backend-publish
RUN dotnet publish src/backend/TeamPulse.Api/TeamPulse.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM backend-restore AS migration-build
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.12
RUN Authentication__Jwt__SigningKey=BuildOnlySigningKey_AtLeast32Bytes_Long \
    ConnectionStrings__TeamPulse="Server=localhost;Database=TeamPulse;User Id=sa;Password=BuildOnlyPassword_123!;TrustServerCertificate=True" \
    /tools/dotnet-ef migrations bundle \
    --project src/backend/TeamPulse.Infrastructure \
    --startup-project src/backend/TeamPulse.Api \
    --configuration Release \
    --target-runtime linux-x64 \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend-publish /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot
COPY --from=migration-build /app/efbundle ./efbundle
COPY render-entrypoint.sh ./render-entrypoint.sh

RUN chmod 755 ./efbundle ./render-entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

USER $APP_UID
ENTRYPOINT ["./render-entrypoint.sh"]
