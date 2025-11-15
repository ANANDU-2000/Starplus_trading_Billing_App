# Backend Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FrozenApi.csproj", "."]
RUN dotnet restore "./FrozenApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "FrozenApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FrozenApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install Arabic and multilingual fonts for PDF generation
# fonts-noto-core provides Noto Sans Arabic for proper Arabic text rendering
RUN apt-get update && apt-get install -y \
    fontconfig \
    fonts-liberation \
    fonts-dejavu-core \
    fonts-freefont-ttf \
    fonts-noto \
    fonts-noto-core \
    fonts-noto-ui-core \
    fonts-noto-cjk \
    && fc-cache -f -v \
    && rm -rf /var/lib/apt/lists/*

# Create uploads directory
RUN mkdir -p /app/wwwroot/uploads

# Copy entrypoint script
COPY docker-entrypoint.sh /app/
RUN chmod +x /app/docker-entrypoint.sh

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
# Don't override PORT - let Render set it via environment variable
# ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["/app/docker-entrypoint.sh"]
