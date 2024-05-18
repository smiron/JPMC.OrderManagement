ARG netVersion=8.0

FROM mcr.microsoft.com/dotnet/sdk:${netVersion} AS build
ARG mainProject

WORKDIR /app/src

COPY ./src/JPMC.OrderManagement.Common/JPMC.OrderManagement.Common.csproj ./JPMC.OrderManagement.Common/JPMC.OrderManagement.Common.csproj
COPY ./src/$mainProject/$mainProject.csproj ./$mainProject/$mainProject.csproj

RUN dotnet restore ./JPMC.OrderManagement.Common/JPMC.OrderManagement.Common.csproj
RUN dotnet restore ./$mainProject/$mainProject.csproj

COPY ./src/JPMC.OrderManagement.Common/* ./JPMC.OrderManagement.Common/
COPY ./src/$mainProject/* ./$mainProject/

WORKDIR /app/src/$mainProject
RUN dotnet build -c Release --no-restore -o /app/build 

FROM build AS publish
ARG mainProject
WORKDIR /app/src/$mainProject
RUN dotnet publish --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:${netVersion} AS service
ARG mainProject
RUN apt update && apt upgrade
ENV mainProject=$mainProject
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT dotnet $mainProject.dll

FROM mcr.microsoft.com/dotnet/aspnet:${netVersion} AS restapi
ARG mainProject
RUN apt update && apt upgrade
ENV mainProject=$mainProject
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT dotnet $mainProject.dll
