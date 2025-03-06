# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["src/Ellipse.Server/Ellipse.Server.csproj", "src/Ellipse.Server/"]
RUN dotnet restore "src/Ellipse.Server/Ellipse.Server.csproj"

# Copy the entire source code and build the app
COPY . .
WORKDIR "/src/src/Ellipse.Server"

RUN dotnet publish "Ellipse.Server.csproj" -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy the published output from build stage
COPY --from=build /app/publish /app

RUN ls -la /app

EXPOSE 8000

ENTRYPOINT ["dotnet", "Ellipse.Server.dll"]
