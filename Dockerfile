# 1. Use the .NET 10 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# 2. Use the .NET 10 Runtime to run the app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# 3. Tell the cloud server to run your specific app
ENTRYPOINT ["dotnet", "SavedUserData.dll"]