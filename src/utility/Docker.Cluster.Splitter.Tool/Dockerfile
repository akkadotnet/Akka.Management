FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/utility/Docker.Cluster.Splitter/Docker.Cluster.Splitter.csproj", "Docker.Cluster.Splitter/"]
RUN dotnet restore "src/utility/Docker.Cluster.Splitter/Docker.Cluster.Splitter.csproj"
COPY . .
WORKDIR "/src/Docker.Cluster.Splitter"
RUN dotnet build "Docker.Cluster.Splitter.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Docker.Cluster.Splitter.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Docker.Cluster.Splitter.dll"]
