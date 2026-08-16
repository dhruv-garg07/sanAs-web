# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["SanAsPrime.csproj", "./"]
RUN dotnet restore "SanAsPrime.csproj"

# Copy source code and build
COPY . .
RUN dotnet publish "SanAsPrime.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy output from build stage
COPY --from=build /app/publish .

# Default port configuration (Render will override via PORT env var)
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "SanAsPrime.dll"]
