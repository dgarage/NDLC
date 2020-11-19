#!/bin/bash

VERSION_FILE="/source/NDLC.CLI/NDLC.CLI.csproj"
VERSION="$(cat $VERSION_FILE | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p')"