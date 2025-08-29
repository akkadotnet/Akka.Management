FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install DNS utilities and diagnostic tools
RUN apt-get update && apt-get install -y \
    dnsutils \
    iputils-ping \
    net-tools \
    && rm -rf /var/lib/apt/lists/*
# Copy published app
COPY bin/Release/net9.0/linux-x64/publish/ /app

# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Configure entry point
# ENTRYPOINT ["dotnet", "/app/DnsCluster.dll"]
ENTRYPOINT ["/entrypoint.sh"]