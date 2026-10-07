# Multi-stage Dockerfile for Restaurant POS Web & Server
# Stage 1: Build Web SPA Frontend
FROM node:22-alpine AS build-web
WORKDIR /app/web
COPY web_server/RestaurantPOS.Web/package*.json ./
RUN npm ci
COPY web_server/RestaurantPOS.Web/ ./
RUN npm run build

# Stage 2: Build .NET 10 Web API Server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
WORKDIR /app
COPY web_server/RestaurantPOS.Shared/ RestaurantPOS.Shared/
COPY web_server/RestaurantPOS.Server/ RestaurantPOS.Server/
RUN dotnet publish RestaurantPOS.Server/RestaurantPOS.Server.csproj -c Release -o /app/publish

# Copy built frontend assets into ASP.NET Core wwwroot
COPY --from=build-web /app/web/dist /app/publish/wwwroot

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build-server /app/publish ./
EXPOSE 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "RestaurantPOS.Server.dll"]
