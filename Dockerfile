FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY PropertySystem.csproj .
RUN dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.*
RUN dotnet restore

# Copy everything else
COPY . .

# Switch to SQLite
RUN sed -i 's/\.UseSqlServer/.UseSqlite/' Program.cs
RUN sed -i 's|"Server=.*TrustServerCertificate=True"|"Data Source=PropertySystem.db"|' appsettings.json

# Build and publish
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PropertySystem.dll"]
