# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies.
COPY ["src/Ellipse.Server/Ellipse.Server.csproj", "src/Ellipse.Server/"]
RUN dotnet restore "src/Ellipse.Server/Ellipse.Server.csproj"

# Copy the remaining source code and build/publish the app
COPY . .
WORKDIR "/src/src/Ellipse.Server"
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8000

ENTRYPOINT ["dotnet", "run"]
