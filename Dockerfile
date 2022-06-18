FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy everything
COPY ./SimpleMailArchiver ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish --runtime alpine-x64 -c Release --self-contained true -o out /p:PublishSingleFile=true

# Build runtime image
FROM alpine:3.9.4

# Add some libs required by .NET runtime 
RUN apk add --no-cache libstdc++ libintl

EXPOSE 80
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1
ENV SMA_ARCHIVEBASEPATH "/etc/mailarchive/"
ENV SMA_IMPORTBASEPATH "/etc/mailimport/"
ENV SMA_ACCOUNTSCONFIGPATH "/etc/mailaccounts"
ENV SMA_DBPATH "/etc/maildb"

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "SimpleMailArchiver.dll"]
