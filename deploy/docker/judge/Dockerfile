FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install --yes --no-install-recommends g++ \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 1000 --shell /usr/sbin/nologin mastemis

USER mastemis
WORKDIR /workspace
ENTRYPOINT ["/usr/bin/true"]
