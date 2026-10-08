FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/EzNihongo.Platform.Api/EzNihongo.Platform.Api.csproj src/EzNihongo.Platform.Api/
RUN dotnet restore src/EzNihongo.Platform.Api/EzNihongo.Platform.Api.csproj

COPY src/EzNihongo.Platform.Api/ src/EzNihongo.Platform.Api/
RUN dotnet publish src/EzNihongo.Platform.Api/EzNihongo.Platform.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY db ./db

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "EzNihongo.Platform.Api.dll"]
