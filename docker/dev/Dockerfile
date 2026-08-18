FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

COPY --from=node /usr/local/ /usr/local/

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        git \
        jq \
        postgresql-client \
        procps \
    && rm -rf /var/lib/apt/lists/*

RUN npm install --global \
        @wonderwhy-er/desktop-commander@0.2.47 \
        @playwright/test@1.62.1

RUN useradd --create-home --uid 1000 --shell /bin/bash agent \
    && mkdir -p \
        /workspace \
        /home/agent/.nuget/packages \
        /home/agent/.RiderDebuggerTools \
        /home/agent/.aspnet/DataProtection-Keys \
    && chown -R agent:agent /workspace /home/agent

USER agent
WORKDIR /workspace
ENV DOTNET_CLI_HOME=/home/agent \
    NUGET_PACKAGES=/home/agent/.nuget/packages \
    NODE_PATH=/usr/local/lib/node_modules

CMD ["bash"]
