# Build context is the repository root: docker build -f src/DocsBrain.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/DocsBrain.Core/DocsBrain.Core.csproj src/DocsBrain.Core/
COPY src/DocsBrain.Api/DocsBrain.Api.csproj src/DocsBrain.Api/
RUN dotnet restore src/DocsBrain.Api/DocsBrain.Api.csproj
COPY src/DocsBrain.Core src/DocsBrain.Core
COPY src/DocsBrain.Api src/DocsBrain.Api
RUN dotnet publish src/DocsBrain.Api/DocsBrain.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DocsBrain.Api.dll"]
