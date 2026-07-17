FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY ["ProductService.csproj", "."]
RUN dotnet restore "ProductService.csproj"

FROM restore AS build
COPY . .
RUN dotnet build "ProductService.csproj" -c Release --no-restore -o /app/build

FROM build AS publish
RUN dotnet publish "ProductService.csproj" -c Release --no-build -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ProductService.dll"]
