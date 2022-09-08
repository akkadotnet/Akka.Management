FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
WORKDIR /app

# should be a comma-delimited list
ENV CLUSTER_SEEDS "[]"
ENV CLUSTER_IP ""
ENV CLUSTER_PORT "4053"

# 9110 - Petabridge.Cmd
# 4055 - Akka.Remote
# 8558 - Akka.Management
EXPOSE 9110 4053 8558

# Install Petabridge.Cmd client so it can be invoked remotely via
# Docker or K8s 'exec` commands
RUN dotnet tool install --global pbm 

# RUN pbm help

COPY ./bin/Release/net6.0/publish/ /app

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS app
WORKDIR /app

COPY --from=base /app /app

# copy .NET Core global tool
COPY --from=base /root/.dotnet /root/.dotnet/

# Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
ENV PATH="${PATH}:/root/.dotnet/tools"

# Needed because https://github.com/aws/amazon-ecs-agent/issues/3299
# Fix was https://github.com/dotnet/coreclr/pull/16141
ENV COMPlus_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "Aws.Ecs.dll"]