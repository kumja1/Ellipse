# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files (include referenced projects)
COPY ["src/Ellipse.Server/Ellipse.Server.csproj", "src/Ellipse.Server/"]
COPY ["src/Ellipse.Common/Ellipse.Common.csproj", "src/Ellipse.Common/"]

# Restore dependencies
RUN dotnet restore "src/Ellipse.Server/Ellipse.Server.csproj"

# Copy the entire source code
COPY . .

WORKDIR "/src/src/Ellipse.Server"
RUN dotnet publish "Ellipse.Server.csproj" -c Release -o /app/publish

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
CMD ["dotnet", "Ellipse.Server.dll"]
