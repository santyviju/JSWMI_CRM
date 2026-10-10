# ==============================================================================
# JSWMI_CRM Dockerfile for Cloud Hosting
# Target: .NET 8.0 ASP.NET Core Razor Pages Web Application
# ==============================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore NuGet dependencies
COPY ["JSWMI_CRM.csproj", "./"]
RUN dotnet restore "JSWMI_CRM.csproj"

# Copy source and publish release build
COPY . .
RUN dotnet publish "JSWMI_CRM.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Standard cloud container port configuration (.NET 8 default is 8080)
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "JSWMI_CRM.dll"]
