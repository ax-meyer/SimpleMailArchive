FROM mcr.microsoft.com/dotnet/sdk AS build-env
WORKDIR /app

# Copy everything
COPY ./SimpleMailArchiver ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet
WORKDIR /app
COPY --from=build-env /app/out .
ENV SMA_ARCHIVEBASEPATH "/etc/mailarchive/"
ENV SMA_IMPORTBASEPATH "/etc/mailimport/"
ENV SMA_ACCOUNTSCONFIGPATH "/etc/mailaccounts"
ENV SMA_DBPATH "/etc/maildb"

ENTRYPOINT ["dotnet", "SimpleMailArchiver.dll"]
