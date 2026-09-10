FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY FinApp.Api.csproj .
RUN dotnet restore FinApp.Api.csproj

COPY . .
RUN dotnet publish FinApp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# A porta já é definida via Kestrel:Endpoints:Http:Url em appsettings.json
EXPOSE 3001

ENTRYPOINT ["dotnet", "FinApp.Api.dll"]
