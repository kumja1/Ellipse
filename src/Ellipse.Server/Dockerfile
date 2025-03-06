# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Copy the project file and restore dependencies (project file is in the same folder)
COPY ["Ellipse.Server.csproj", "."]
RUN dotnet restore "Ellipse.Server.csproj"

# Copy the remaining source code and build/publish the app
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8000

ENTRYPOINT ["dotnet", "Ellipse.Server.dll"]
