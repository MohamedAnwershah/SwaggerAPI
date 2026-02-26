# 1. Use the .NET 9 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# 2. Use the .NET 9 Runtime to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# 3. Tell the cloud server to run your specific app
# IMPORTANT: Change "SavedUserData.dll" to whatever your .csproj file is named!
# (e.g., if your file is NutriChefAPI.csproj, use "NutriChefAPI.dll")
ENTRYPOINT ["dotnet", "SavedUserData.dll"]