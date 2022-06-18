FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy everything
COPY ./SimpleMailArchiver ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish --runtime alpine-x64 -c Release -o out 

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

WORKDIR /app

ENV SMA_ARCHIVEBASEPATH "/etc/mailarchive/"
ENV SMA_IMPORTBASEPATH "/etc/mailimport/"
ENV SMA_ACCOUNTSCONFIGPATH "/etc/mailaccounts"
ENV SMA_DBPATH "/etc/maildb"

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "SimpleMailArchiver.dll"]
