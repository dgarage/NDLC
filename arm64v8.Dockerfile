# This is a manifest image, will pull the image with the same arch as the builder machine
FROM mcr.microsoft.com/dotnet/core/sdk:3.1.202 AS builder
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV LC_ALL en_US.UTF-8
RUN apt-get update \
	&& apt-get install -qq --no-install-recommends qemu qemu-user-static qemu-user binfmt-support

WORKDIR /source
COPY NDLC/NDLC.csproj NDLC/NDLC.csproj
COPY NDLC.CLI/NDLC.CLI.csproj NDLC.CLI/NDLC.CLI.csproj
RUN cd NDLC.CLI && dotnet restore
COPY NDLC/. NDLC/.
COPY NDLC.CLI/. NDLC.CLI/.
ARG CONFIGURATION_NAME=Release
RUN cd NDLC.CLI && dotnet publish --output /app/ --configuration ${CONFIGURATION_NAME}

# Force the builder machine to take make an arm runtime image. This is fine as long as the builder does not run any program
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.4-buster-slim-arm64v8
COPY --from=builder /usr/bin/qemu-aarch64-static /usr/bin/qemu-aarch64-static
ENV LC_ALL en_US.UTF-8
ENV LANG en_US.UTF-8

WORKDIR /root/.ndlc
WORKDIR /app
ENV NDLC_DATADIR=/root/.ndlc
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME /root/.ndlc

COPY --from=builder "/app" .
ENTRYPOINT ["dotnet", "ndlc-cli.dll"]
