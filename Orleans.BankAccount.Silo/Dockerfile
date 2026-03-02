# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Orleans.BankAccount.sln", "./"]
COPY ["Orleans.BankAccount.Silo/Orleans.BankAccount.Silo.csproj", "Orleans.BankAccount.Silo/"]
COPY ["Orleans.BankAccount.Grains/Orleans.BankAccount.Grains.csproj", "Orleans.BankAccount.Grains/"]
COPY ["Orleans.BankAccount.Interfaces/Orleans.BankAccount.Interfaces.csproj", "Orleans.BankAccount.Interfaces/"]

# Restore dependencies
RUN dotnet restore "Orleans.BankAccount.Silo/Orleans.BankAccount.Silo.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/Orleans.BankAccount.Silo"
RUN dotnet build "Orleans.BankAccount.Silo.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Orleans.BankAccount.Silo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Expose ports
# 8081 - HTTP/REST API
# 11111 - Orleans Silo-to-Silo
# 30000 - Orleans Client Gateway
EXPOSE 8081 11111 30000

# Set entry point
ENTRYPOINT ["dotnet", "Orleans.BankAccount.Silo.dll"]

