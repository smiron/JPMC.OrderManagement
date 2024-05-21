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
RUN apt update && apt upgrade
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "entrypoint.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${netVersion} AS restapi
RUN apt update && apt upgrade
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "entrypoint.dll"]
