# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["BpTracker.Api.csproj", "."]
RUN dotnet restore "BpTracker.Api.csproj"
COPY . .
RUN dotnet build "BpTracker.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BpTracker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BpTracker.Api.dll"]
