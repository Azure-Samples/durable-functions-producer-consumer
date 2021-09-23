# Find the Dockerfile for mcr.microsoft.com/azure-functions/dotnet:3.0-dotnet3-core-tools at this URL
# https://github.com/Azure/azure-functions-docker/blob/main/host/3.0/buster/amd64/dotnet/dotnet-core-tools.Dockerfile
FROM mcr.microsoft.com/azure-functions/dotnet:3.0-dotnet3-core-tools

# Uncomment following lines If you want to enable Development Container Script
# For more details https://github.com/microsoft/vscode-dev-containers/tree/main/script-library

# Avoid warnings by switching to noninteractive
ENV DEBIAN_FRONTEND=noninteractive

RUN \
    # Install NPM
    wget https://deb.nodesource.com/setup_16.x | bash \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        npm \
        jq \
    #
    # Install .NET 6.0
    && wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && sudo chmod +x dotnet-install.sh \
    && ./dotnet-install.sh -c 6.0 -v latest --no-path --install-dir /usr/share/dotnet \
    #
    # Rename current 'func' bin -> 'func3'
    && mv /usr/bin/func /usr/bin/func3 \
    #
    # Install Func core tools v4 via NPM
    && sudo npm i -g azure-functions-core-tools@4 --unsafe-perm true \
    #
    # Clean up
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

# Switch back to dialog for any ad-hoc use of apt-get
ENV DEBIAN_FRONTEND=dialog
