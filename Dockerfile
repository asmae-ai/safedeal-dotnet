FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SafeDeal.API/SafeDeal.API.csproj", "SafeDeal.API/"]
COPY ["SafeDeal.Application/SafeDeal.Application.csproj", "SafeDeal.Application/"]
COPY ["SafeDeal.Domain/SafeDeal.Domain.csproj", "SafeDeal.Domain/"]
COPY ["SafeDeal.Infrastructure/SafeDeal.Infrastructure.csproj", "SafeDeal.Infrastructure/"]
RUN dotnet restore "SafeDeal.API/SafeDeal.API.csproj"
COPY . .
WORKDIR "/src/SafeDeal.API"
RUN dotnet build "SafeDeal.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SafeDeal.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SafeDeal.API.dll"]