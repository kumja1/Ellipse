# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["Ellipse.Server/Ellipse.Server.csproj", "Ellipse.Server/"]
RUN dotnet restore "Ellipse.Server/Ellipse.Server.csproj"

# Copy the remaining source code and build/publish the app
COPY . .
WORKDIR "/src/Ellipse.Server"
RUN dotnet publish -c Release -o /app/publish


# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8000

ENTRYPOINT ["dotnet", "Ellipse.Server.dll"]
