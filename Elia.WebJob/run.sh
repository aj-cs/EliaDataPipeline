#!/bin/bash
set -e
cd "$(dirname "$0")"
dotnet Elia.WebJob.dll
