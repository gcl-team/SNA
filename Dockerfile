# Use official .NET SDK image to build the app
# Reference: https://mcr.microsoft.com/artifact/mar/dotnet/sdk/tags
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-preview.4 AS build

WORKDIR /src

# Copy csproj and restore as distinct layers for caching
COPY SimNextgenApp/*.csproj ./SimNextgenApp/
COPY SimNextgenApp.Demo/*.csproj ./SimNextgenApp.Demo/
RUN dotnet restore SimNextgenApp.Demo/SimNextgenApp.Demo.csproj

# Copy all source files and build the app
COPY . .
RUN dotnet publish SimNextgenApp.Demo/SimNextgenApp.Demo.csproj -c Release -o /app/publish

# Use the official .NET runtime image for the final container
# Reference: https://mcr.microsoft.com/artifact/mar/dotnet/runtime/tags
FROM mcr.microsoft.com/dotnet/runtime:10.0.0-preview.4

WORKDIR /app

COPY --from=build /app/publish .

LABEL maintainer="GCL Team"
LABEL description="SimNextgenApp Demo Console Application"

ENTRYPOINT ["dotnet", "SimNextgenApp.Demo.dll"]
