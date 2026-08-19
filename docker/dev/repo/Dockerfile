FROM node:22-bookworm-slim AS node
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS dotnet
FROM ghcr.io/coder/code-server:4.131.0@sha256:3623e6362abdec6258472882b06fdeec9d6ce2ad3fda316b3c5d7ed092b89add

USER root

COPY --from=node /usr/local/ /usr/local/
COPY --from=dotnet /usr/share/dotnet/ /usr/share/dotnet/
COPY --chmod=755 start-code-server.sh /usr/local/bin/start-code-server

RUN ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        jq \
        libicu76 \
        postgresql-client \
        procps \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/coder/.nuget/packages /workspace \
    && chown -R coder:coder /home/coder /workspace

USER coder
ENV DOTNET_CLI_HOME=/home/coder \
    NUGET_PACKAGES=/home/coder/.nuget/packages \
    NODE_PATH=/usr/local/lib/node_modules

WORKDIR /workspace
ENTRYPOINT ["/usr/local/bin/start-code-server"]
