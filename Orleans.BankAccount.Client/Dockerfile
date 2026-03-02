FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Orleans.BankAccount.Client/Orleans.BankAccount.Client.csproj", "Orleans.BankAccount.Client/"]
RUN dotnet restore "Orleans.BankAccount.Client/Orleans.BankAccount.Client.csproj"
COPY . .
WORKDIR "/src/Orleans.BankAccount.Client"
RUN dotnet build "./Orleans.BankAccount.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Orleans.BankAccount.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Orleans.BankAccount.Client.dll"]
