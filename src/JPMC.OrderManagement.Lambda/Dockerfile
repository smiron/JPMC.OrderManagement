ARG netVersion=8.0

FROM mcr.microsoft.com/dotnet/runtime:${netVersion} AS base

FROM mcr.microsoft.com/dotnet/sdk:${netVersion} AS build
ARG mainProject

WORKDIR /app/src/$mainProject
COPY ./$mainProject/$mainProject.csproj .
RUN dotnet restore

WORKDIR /app/src
COPY ./$mainProject ./$mainProject

WORKDIR /app/src/$mainProject
RUN dotnet build -c Release --no-restore -o /app/build 

FROM build AS publish
ARG mainProject
WORKDIR /app/src/$mainProject
RUN dotnet publish --no-restore -c Release -o /app/publish