# Prerequisites and Assumptions

## Database

As you may have noticed, this setup does not define a database.
The backend API uses a PostgreSQL database, and is assumed to be present and reachable during runtime.
The connection string present in this repository in some `appsettings.json` files and the [`.env`](../.env) file point to a locally hosted PostgreSQL instance.

To replicate the current setup, first:
1. Get a PostgreSQL database running (e.g., using [PostgreSQL Docker container](https://hub.docker.com/_/postgres) or install PostgreSQL locally)
2. Obtain a connection string. You may need to create a database and set up users. For Docker: `docker run --name postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=AspTemplate -p 5432:5432 -d postgres`
3. Replace the placeholder connection strings present in the [`.env`](../.env) file and in some `appsettings.json` files
4. Set up the database:

Execute the [`reset_db.ps1`](../src/WebApi.Seeding/reset_db.ps1) script in the seeding project.
This resets the schema and tables for the specified database, then inserts test data into the newly constructed database.
**Even if the database does not contain the required tables, it will construct and populate them.**

## Teamcity

This repository assumes you have some sort of [TeamCity](https://www.jetbrains.com/teamcity/) instance running, whether locally or on the cloud.

### Installing Teamcity

If you've never used TeamCity before, it is a CI/CD platform similar to [Jenkins](https://www.jenkins.io/).
However, it is not open-source, and the code is proprietary.
TeamCity has multiple setups, with only the ["On-Premises" Professional](https://www.jetbrains.com/teamcity/buy/?edition=on-premises) being free to use.
The free version contains limitations, with limited build configurations being one of them.
However, if you intend to use it for personal or small-scale use, this shouldn't be too limiting.

It is recommended to use existing TeamCity [server](https://hub.docker.com/r/jetbrains/teamcity-server) and [agent](https://hub.docker.com/r/jetbrains/teamcity-agent) images.
The server provides the UI, whilst the agent executes defined build configurations.
It is worth noting that **agents do not come with batteries fully included**, and require configuration to include the tools required to perform tasks.
For example, the image does not have .NET, meaning it's best practice to "extend" the agent's configuration to include .NET as well as other required tools such as git.

```dockerfile
FROM jetbrains/teamcity-agent:2025.07

USER root

# common tools
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    unzip \
    apt-transport-https \
    ca-certificates \
    gnupg \
    lsb-release \
    jq \
    moreutils \
    git

# installing .NET 9
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-9.0
```

As such, it is usually best to define our current structure inside a `docker-compose.yml` file which references the aforementioned modified TeamCity agent.
In the below example, we reference the above agent configuration, whilst also creating volumnes so that we don't have to re-configure TeamCity after every rebuild:

```dockerfile
services:
  teamcity-server:
    image: jetbrains/teamcity-server:2025.11.1
    container_name: teamcity-server
    ports:
      - "8111:8111"
    volumes:
      - ./teamcity-backups:/data/teamcity_server/datadir/backup:Z 
      - teamcity-data:/data/teamcity_server/datadir
      - teamcity-logs:/opt/teamcity/logs
    restart: unless-stopped

  teamcity-agent:
    build:
      context: .
      dockerfile: Dockerfile.agent
    container_name: teamcity-agent
    environment:
      - SERVER_URL=http://teamcity-server:8111
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./service-account.json:/opt/gcp/service-account.json:ro,Z
    depends_on:
      - teamcity-server
    restart: unless-stopped
    entrypoint: ["/entrypoint-wrapper.sh"]
    command: >
      sh -c "
        /opt/buildagent/bin/setup-gcp.sh &&
        /run-services.sh
      "

volumes:
  teamcity-data:
  teamcity-logs:
```

From there, we can launch our config using:

```bash
docker compose build
docker compose up -d
docker compose logs -f
```

### Agent Configuration
