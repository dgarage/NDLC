FROM mcr.microsoft.com/dotnet/core/sdk:3.1.202 AS builder
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source
COPY NDLC/NDLC.csproj NDLC/NDLC.csproj
COPY NDLC.CLI/NDLC.CLI.csproj NDLC.CLI/NDLC.CLI.csproj
COPY NDLC.Tests/NDLC.Tests.csproj NDLC.Tests/NDLC.Tests.csproj
RUN cd NDLC.Tests && dotnet restore
COPY NDLC/. NDLC/.
COPY NDLC.CLI/. NDLC.CLI/.
COPY NDLC.Tests/. NDLC.Tests/.
RUN cd NDLC.Tests && dotnet test -v n