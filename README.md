# Mango.Services.AuthAPI

Mango.Services.AuthAPI is the authentication and user-management microservice in the Mango application. It handles user registration, login, role assignment, and JWT generation. It uses ASP.NET Core Identity and Entity Framework Core with SQL Server for user persistence.

## Contents

- [Overview](#overview)
- [Technology](#technology)
- [Features](#features)
- [Registration Message Flow](#registration-message-flow)
- [Shared MessageBus v3.0.0 Integration](#shared-messagebus-v300-integration)
- [Configuration](#configuration)
- [Prerequisites](#prerequisites)
- [Run Locally](#run-locally)
- [Run with Docker](#run-with-docker)
- [Run in the Mango Compose Stack](#run-in-the-mango-compose-stack)
- [CI/CD](#cicd)
- [Troubleshooting](#troubleshooting)

## Overview

AuthAPI provides authentication functionality for Mango.Web and other services that rely on Mango user accounts.

The service uses ASP.NET Core Identity to manage users and roles, SQL Server to persist identity data, and JSON Web Tokens (JWTs) to represent authenticated users.

AuthAPI also publishes registration-related messages to RabbitMQ through the shared `Mango.MessageBus` package. This allows downstream services, such as EmailAPI, to consume registration messages independently of the registration request.

## Technology

- .NET 10 / ASP.NET Core
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- JWT authentication tokens
- RabbitMQ
- `Mango.MessageBus` NuGet package
- DotNetEnv for local environment configuration
- Docker and Docker Compose
- GitHub Actions

## Features

### User Registration

Creates user accounts through ASP.NET Core Identity and stores user information in SQL Server. After successful registration, the service publishes a user-registration message to the configured RabbitMQ queue.

### User Login

Validates user credentials and returns a response containing user information and a JWT when authentication succeeds.

### JWT Generation

Generates signed JWTs containing user identity, email, username, and role claims. The issuer, audience, and signing secret are supplied through configuration.

### Role Assignment

Provides an endpoint for assigning roles to users through the authentication service.

## Registration Message Flow

```text
Mango.Web
    |
    | POST /api/auth/register
    v
Mango.Services.AuthAPI
    |
    +--> ASP.NET Core Identity
    |          |
    |          v
    |      SQL Server
    |
    +--> Mango.MessageBus
               |
               v
            RabbitMQ
               |
               v
            EmailAPI
```

After a user is successfully registered, AuthAPI publishes a `UserDto` containing the user's name, email, and phone number through `IMessageProducer` from `Mango.MessageBus`.

The destination queue is configured through `TopicAndQueueNames:RegisterUserQueue`.

The queue name and RabbitMQ connection settings must match the configuration used by the corresponding consumer. A successful registration response does not, by itself, prove that the downstream message has been consumed.

## Shared MessageBus v3.0.0 Integration

AuthAPI now uses the shared `Mango.MessageBus` NuGet package for RabbitMQ publishing instead of maintaining its own RabbitMQ sender interface and implementation. The project references `Mango.MessageBus` version `3.0.0` and targets .NET 10.

### Dependency injection

`Program.cs` binds the RabbitMQ settings from `MessageQueue:RabbitMQ` and registers the producer as a singleton:

```csharp
builder.Services
    .AddOptions<RabbitMqSettings>()
    .Bind(builder.Configuration.GetSection("MessageQueue:RabbitMQ"))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.HostName),
        "RabbitMQ HostName is required.")
    .Validate(
        settings => settings.Port is >= 1 and <= 65535,
        "RabbitMQ Port must be between 1 and 65535.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.UserName),
        "RabbitMQ UserName is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Password),
        "RabbitMQ Password is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.VirtualHost),
        "RabbitMQ VirtualHost is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthQueueOptions>()
    .Bind(builder.Configuration.GetSection("TopicAndQueueNames"))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.RegisterUserQueue),
        "TopicAndQueueNames:RegisterUserQueue is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<IMessageProducer, RabbitMQMessageProducer>();
```
The RabbitMQ settings are bound from `MessageQueue:RabbitMQ` and validated at startup. The registration queue is bound from `TopicAndQueueNames` and must be configured before the application starts. `IMessageProducer` is registered as a singleton. Supply the required connection settings and queue name through the active configuration profile.

### Registration message publishing

After successful user registration, AuthAPI creates a `UserDto` and publishes it through `IMessageProducer`. The destination queue is read from `TopicAndQueueNames:RegisterUserQueue`. AuthAPI supplies an explicit message ID and queue options when publishing. The current configuration declares the queue as durable and supplies these dead-letter arguments:

- `x-dead-letter-exchange`: `email.dlx`
- `x-dead-letter-routing-key`: the configured registration queue name

The `email.dlx` exchange must exist and the dead-letter destination and bindings must be configured consistently with the corresponding consumer and deployment topology. Publishers and consumers must agree on queue durability and arguments; redeclaring an existing queue with incompatible settings can cause RabbitMQ to return `PRECONDITION_FAILED` (406). Coordinate any queue-topology changes and plan a safe migration before deploying them. Do not delete a queue blindly, because queued messages may be lost.

### GitHub Packages and Docker builds

`Mango.MessageBus` is restored from the private GitHub Packages NuGet feed. Ensure the NuGet configuration used for restore includes the feed and that the build environment has permission to download the package. For Docker builds, the Dockerfile expects a BuildKit secret named `nugetconfig`; the secret provides NuGet configuration during restore without copying credentials into the image. Set `NUGET_CONFIG_PATH` to a valid NuGet configuration file before using the documented Docker build command. Do not commit a real token or a credential-bearing NuGet configuration.

### v3.0.0 consumer migration notes

- Use a .NET 10-compatible target framework.
- Register `IMessageProducer` as a singleton.
- Pass a non-empty message ID to `SendMessage`.
- Supply `RabbitMqQueueOptions` that match the intended queue topology.
- Verify the GitHub Packages feed and credentials for local, CI, and Docker restores.
- Test the publisher and consumer together after package or queue configuration changes.

## Configuration

The service uses environment-specific configuration for local execution and Docker.

The active profile is selected using `MANGO_PROFILE`. If it is not set, the application defaults to `Http` outside a container and `Docker` inside a container.

For local execution, the application loads `.env`. Configuration uses the `Http__` or `Docker__` environment-variable prefix according to the active profile.

Start with `.env.example` and supply the values required for your environment. Do not commit secrets or real credentials.

### JWT Settings

Configure the following values:

| Variable | Purpose |
|---|---|
| `ApiSettings__Secret` | Secret used to sign JWTs |
| `ApiSettings__Issuer` | JWT issuer |
| `ApiSettings__Audience` | JWT audience |

These values must be compatible with the settings used by services that validate AuthAPI's tokens.

### SQL Server Settings

Provide the connection string for the selected profile:

```text
Http__ConnectionStrings__DefaultConnection
Docker__ConnectionStrings__DefaultConnection
```

For local execution, use a SQL Server hostname reachable from the host.

For container execution, use a hostname reachable from inside the AuthAPI container. For example, `host.docker.internal` may be appropriate when SQL Server runs on the Docker host and its port is published.

The application applies pending Entity Framework Core migrations during startup. The database must be reachable, and the configured account must have the necessary migration permissions.

### RabbitMQ Settings

AuthAPI uses the `MessageQueue:RabbitMQ` configuration section to configure the shared message producer.

The relevant environment-variable names include:

```text
MessageQueue__RabbitMQ__HostName
MessageQueue__RabbitMQ__Port
MessageQueue__RabbitMQ__VirtualHost
MessageQueue__RabbitMQ__UserName
MessageQueue__RabbitMQ__Password
```

The host can also be supplied through the active profile, for example:

```text
Http__MessageQueue__RabbitMQ__HostName
Docker__MessageQueue__RabbitMQ__HostName
```

Set the registration queue name using:

```text
TopicAndQueueNames__RegisterUserQueue
```

The configured queue must match the queue consumed by EmailAPI. Supply valid RabbitMQ credentials and connection details for the selected environment.

## Prerequisites

- .NET 10 SDK
- SQL Server
- RabbitMQ for registration messaging
- Access to the private GitHub Packages NuGet feed for `Mango.MessageBus`
- Docker Desktop for container execution

## Run Locally

1. Navigate to the AuthAPI project directory.
2. Copy `.env.example` to `.env`.
3. Configure the database connection, JWT settings, RabbitMQ settings, and registration queue.
4. Ensure SQL Server and RabbitMQ are reachable.
5. Restore, build, and run the application.

Run the following commands from the project directory:

```powershell
dotnet restore Mango.Services.AuthAPI.csproj
dotnet build Mango.Services.AuthAPI.csproj
dotnet run --launch-profile http
```

The HTTP launch profile uses `http://localhost:5005` and launches Swagger in Development.

If restoring `Mango.MessageBus` fails, verify that the GitHub Packages NuGet source is configured and that you have permission to access the package.

## Run with Docker

The Dockerfile uses .NET 10 SDK and ASP.NET runtime images. The application listens on port `8080` inside the container.

The Docker build uses a BuildKit secret named `nugetconfig` to provide NuGet configuration during package restoration. This is required when restoring the private `Mango.MessageBus` package.

Run these commands from the AuthAPI project directory.

### Build the Image

Set `NUGET_CONFIG_PATH` to the path of a valid NuGet configuration file with access to the required private package feed.

For PowerShell:

```powershell
docker buildx build `
  --secret id=nugetconfig,src="$env:NUGET_CONFIG_PATH" `
  -t mango-authapi:local `
  --load .
```

### Run the Container

Ensure `.env` contains the required configuration values for the container, then run:

```powershell
docker run --rm --name mango-authapi `
  --env-file .env `
  -p 5005:8080 `
  mango-authapi:local
```

The container's port `8080` is mapped to host port `5005`.

Ensure the database and RabbitMQ hostnames are reachable from inside the container. If the dependencies run in other Docker containers, use hostnames reachable over the relevant Docker network.

Do not commit the actual `.env` file or a NuGet configuration containing credentials.

## Run in the Mango Compose Stack

AuthAPI can run as part of the full Mango microservices stack using the solution's Docker Compose configuration.

From the solution root, inspect the resolved Compose configuration and use the actual AuthAPI service name defined in the Compose file.

```powershell
docker compose config
```

After confirming the service name, use the corresponding commands to build, start, and inspect the service. If the service is named `mango-auth`, for example:

```powershell
docker compose build mango-auth
docker compose up -d mango-auth
docker compose logs -f mango-auth
```

The Compose configuration should supply the required database, JWT, and RabbitMQ settings and connect AuthAPI to the appropriate network.

If SQL Server or RabbitMQ runs as another service in the same Compose network, use that service's Compose hostname. If a dependency runs separately on the Docker host, configure a hostname and port reachable from the container.

## CI/CD

The GitHub Actions workflow is located at `.github/workflows/auth-ci-cd.yml`.

The workflow:

1. Checks out the repository.
2. Sets up the .NET 10 SDK.
3. Caches NuGet packages.
4. Configures the private GitHub Packages source.
5. Restores and builds the AuthAPI project.
6. Runs the configured `dotnet test` step.
7. Creates a temporary NuGet configuration for Docker.
8. Builds the Docker image using Buildx and passes the NuGet configuration through a BuildKit secret.
9. Pushes the image to GitHub Container Registry when the workflow's main-branch push condition is satisfied.
10. Removes the temporary NuGet configuration.

The workflow expects the `GH_PACKAGES_TOKEN` repository secret for private package access.

The Docker build secret ID must remain consistent with the ID declared in the Dockerfile: `nugetconfig`.

The workflow's configured branch and pull-request triggers determine when these steps run.

## Troubleshooting

| Symptom | Checks |
|---|---|
| Database connection fails | Check the active profile, connection string, SQL Server availability, hostname, and port. |
| Registration fails | Check the request payload, Identity validation errors, and database connectivity. |
| RabbitMQ connection fails | Verify the selected hostname, port, username, password, virtual host, and broker availability. |
| Registration messages are not consumed | Confirm `TopicAndQueueNames:RegisterUserQueue` matches EmailAPI's configured registration queue. |
| JWT validation fails in another service | Verify issuer, audience, signing secret, token expiry, and claim handling. |
| Docker image build fails during restore | Check `NUGET_CONFIG_PATH`, the BuildKit secret ID, and private package permissions. |
| Application fails because configuration is missing | Check the active `MANGO_PROFILE` and confirm that all required environment variables are supplied. |
