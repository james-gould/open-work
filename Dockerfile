# syntax=docker/dockerfile:1

# ---- Frontend build -------------------------------------------------------
FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- Backend build ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet restore backend/OpenWork.sln
RUN dotnet publish backend/src/OpenWork.Api/OpenWork.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

RUN mkdir -p /data
VOLUME ["/data"]

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Default="Data Source=/data/openwork.db"
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpenWork.Api.dll"]
