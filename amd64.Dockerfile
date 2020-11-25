FROM mcr.microsoft.com/dotnet/core/sdk:3.1.202 AS builder
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source
COPY NDLC/NDLC.csproj NDLC/NDLC.csproj
COPY NDLC.CLI/NDLC.CLI.csproj NDLC.CLI/NDLC.CLI.csproj
RUN cd NDLC.CLI && dotnet restore
COPY NDLC/. NDLC/.
COPY NDLC.CLI/. NDLC.CLI/.
ARG CONFIGURATION_NAME=Release
RUN cd NDLC.CLI && dotnet publish \
	-p:PublishReadyToRun=true \
	-r linux-x64 \
	--output /app/ --configuration ${CONFIGURATION_NAME}

FROM mcr.microsoft.com/dotnet/runtime-deps:3.1-buster-slim

ENV LC_ALL en_US.UTF-8
ENV LANG en_US.UTF-8

WORKDIR /root/.ndlc
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
ENV NDLC_DATADIR=/root/.ndlc
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME /root/.ndlc

COPY --from=builder "/app" .
ENTRYPOINT ["/app/ndlc-cli"]
