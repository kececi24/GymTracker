FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY backend/GymTracker.Api/GymTracker.Api.csproj backend/GymTracker.Api/
RUN dotnet restore backend/GymTracker.Api/GymTracker.Api.csproj
COPY backend/GymTracker.Api/ backend/GymTracker.Api/
RUN dotnet publish backend/GymTracker.Api/GymTracker.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GymTracker.Api.dll"]
