ARG netVersion=8.0

FROM mcr.microsoft.com/dotnet/runtime:${netVersion} AS base

FROM mcr.microsoft.com/dotnet/sdk:${netVersion} AS build
ARG mainProject

WORKDIR /app/src

# restore dependencies
# start with restoring the dependencies of dependencies as these are less likely to change
COPY ./JPMC.OrderManagement.LambdaHandler/JPMC.OrderManagement.LambdaHandler.csproj ./JPMC.OrderManagement.LambdaHandler/
RUN dotnet restore ./JPMC.OrderManagement.LambdaHandler/JPMC.OrderManagement.LambdaHandler.csproj

COPY ./JPMC.OrderManagement.Utils/JPMC.OrderManagement.Utils.csproj ./JPMC.OrderManagement.Utils/
RUN dotnet restore ./JPMC.OrderManagement.Utils/JPMC.OrderManagement.Utils.csproj

# restore the main project dependencies
COPY ./$mainProject/$mainProject.csproj ./$mainProject/
RUN dotnet restore ./$mainProject/$mainProject.csproj

# copy project files
COPY ./JPMC.OrderManagement.LambdaHandler ./JPMC.OrderManagement.LambdaHandler/
COPY ./JPMC.OrderManagement.Utils ./JPMC.OrderManagement.Utils/
COPY ./$mainProject ./$mainProject/

WORKDIR /app/src/$mainProject
RUN dotnet build -c Release --no-restore -o /app/build 

FROM build AS publish
ARG mainProject
WORKDIR /app/src/$mainProject
RUN dotnet publish --no-restore -c Release -o /app/publish