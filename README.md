# Dotnet.Template

Hello! This is a basic template for a .NET 8 API project, which can be used as a inspirations for new projects. Using the following technologies:

- .NET 8 & C#
- Minimal APIs
- MediatR
- Entity Framework Core
- PostgreSQL
- Docker
- TestContainers
- Serilog
- FluentValidation
- FluentResults

## Architecture

I do not claim this is the "correct way" to architect an API, please feel free to suggest improvements or changes. I hope this template is useful to you!

The architecture is based on the Clean Architecture, and the project is divided into the following layers:

- Api (presentation-layer, and entry point for the application)
- Application (business logic)
- Domain (domain models)
- Infrastructure (database, caching, etc.)

The application also uses the mediator-pattern when communicating between the Api and Application-layer in a de-coupled manner. This is achieved by using the MediatR library. The functional side of this means that the application is divided into commands and queries (see `AddForecastCommand` and `GetForecastQuery`), and the business logic is implemented in handlers (see same folders are command/queries).

### Dependency graph

<img src="./DotnetTemplateDependencyGraph.png" alt="The solution dependency graph" width="500"/>

As can be seen, in the solution Api depends upon Infrastructure, which depends upon the core Application and Domain projects. But in essence Api only does this as the entry point of the application, which is responsible for composing everything through `DependencyInjection`. Other than that, Api should use nothing from Infrastructure, and only depend on Application and Domain.

## Getting started

### Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download)
- [Docker](https://www.docker.com/products/docker-desktop)

### Development

1. Clone this repository on your local machine
1. In the `src/Api` directory of the cloned repository, run `sh setup-jwts.sh`. This command will set up the necessary local JWTs required to communicate with the API. 2. _Note: The script must be run from the same OS/Virtualization as the application will be running from (because of access to dotnet user-secrets)_.
1. Start the API by running the following command in the terminal: `dotnet run --project src/Api/Api.csproj`. Alternatively, you can run the project from your favorite IDE.
1. Once the API is running, open `http://localhost:5062/swagger/` in your web browser to access the Swagger UI.
1. To authenticate with the API, you will need a JWT token that is printed when you run `setup-jwts.sh`. Paste this token into the Swagger UI to start using the API. If you need to retrieve the token again, you can run `dotnet user-jwts print <id of the token>` from the `src/Api` folder.

**Important**: This solution uses [`CSharpier` for formatting the code](https://csharpier.com/). It is installed as a local tool, and will automatically be installed when running `dotnet build` or `dotnet restore`. In addition we use [`Husky.Net`](https://alirezanet.github.io/Husky.Net/) to run the formatter before each commit. It is recommended (but not required) to use a [CSharpier-plugin for your IDE](https://csharpier.com/docs/Editors) to format the code on save, so your code does not unexpectedly change when committing the file.

**Important**: For local development, if `TestContainers:Enabled` is set to `true` in the `appsettings.Development.json`-file (it is by default), the Api will start a PostgreSQL database in a Docker container using TestContainers. This means that Docker must be running when starting the Api.

### Testing

Important: The tests require Docker to be running.

1. Run the tests by running the following command in the terminal: `dotnet test` (or run the tests from your favorite IDE).

The tests are E2E-tests, which means the Api will be actually running in-memory by use of WebApplicationFactory. And a PostgreSQL database will be started in a Docker container using the TestContainers-library. This means the database will be started, and migrations run against it, and the database will be removed when the tests are done.

For each individual test, the database will be reset to a clean state using the Respawn-library.

#### Api

Folder: `src/Api`

##### Routes

The API uses Minimal APIs in this project. The API is configured in `Program.cs` and the routes are defined in the `Routes` folder.

Each group of routes has its own folder, and each endpoint under a group of routes has its own file.

So for the `/weather`-group of routes, you will find the group definition under `Routes/Weather/WeatherGroup.cs`, and the endpoints under `Routes/Weather/Endpoint/GetForecast.cs` and `Routes/Weather/Endpoint/PostForecast.cs`.

In addition any request or response models are defined in the `Models` folder. For example, the request model for the `AddForecast`-endpoint is defined in `Models/Weather/Models/PostWeatherRequest.cs`.

##### Authentication and Authorization

The API uses JWTs for authentication and authorization. The JWTs are validated using the `JwtBearer`-middleware.

The Authorization-policies is set per group of routes or individual endpoints as one chooses, and is defined in the `WeatherGroup.cs`-file.

The policies are defined in the `Authorization/ApiAuthorizationPolicy.cs`-file and added when configuring the services in `Program.cs`.

### Docker Compose (Demo, more CI-friendly)

This repository also includes a `docker-compose.yml` that can run the full stack (PostgreSQL + migrations + API) without Visual Studio, `dotnet user-jwts`, or user-secrets.

#### Prerequisites

- Docker (incl. Docker Compose)

#### 1) Generate a signing key and write it to `.env`

We use a shared HMAC key for HS256 JWT signing. Generate a random 32-byte key (base64) and store it in `.env`:

```bash
umask 077
JWT_KEY_B64="$(openssl rand -base64 32)"
printf "JWT_KEY_B64=%s\n" "$JWT_KEY_B64" > .env
echo "Wrote JWT_KEY_B64 to .env"
```

#### 2) Start PostgreSQL + run migrations + start API

```bash
docker compose up -d postgres
docker compose run --rm migrator
docker compose up api # we keep it in foreground
```

Open another shell and validate health:

```bash
curl -i http://localhost:8080/healthz
```

#### 3) Prepare client environment and mint tokens

The API expects:
- `iss`: `dotnet-user-jwts`
- `aud`: `weather.dev.api`
- `scope`: includes `read` + `write` for user operations, and `admin` for admin operations.

Run this in your shell (same directory as `.env`):

```bash
set -euo pipefail

# Read signing key from .env
JWT_KEY_B64="$(grep '^JWT_KEY_B64=' .env | cut -d= -f2-)"
JWT_KEY_HEX="$(printf '%s' "$JWT_KEY_B64" | base64 -d | xxd -p -c 256)"
export JWT_KEY_B64 JWT_KEY_HEX

b64url() { openssl base64 -e -A | tr '+/' '-_' | tr -d '='; }

mint_jwt () {
  local scopes_json="$1"
  local aud="weather.dev.api"
  local iss="dotnet-user-jwts"
  local now exp header payload signature
  now="$(date +%s)"
  exp="$((now + 3600))" # 1 hour validity

  header="$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)"
  payload="$(printf '{"iss":"%s","aud":"%s","scope":%s,"iat":%d,"exp":%d}' \
    "$iss" "$aud" "$scopes_json" "$now" "$exp" | b64url)"

  signature="$(printf "%s.%s" "$header" "$payload" \
    | openssl dgst -sha256 -mac HMAC -macopt hexkey:"$JWT_KEY_HEX" -binary | b64url)"

  printf "%s.%s.%s\n" "$header" "$payload" "$signature"
}

export READWRITE_TOKEN="$(mint_jwt '["read","write"]')"
export ADMIN_TOKEN="$(mint_jwt '["admin"]')"

echo "READWRITE_TOKEN ready"
echo "ADMIN_TOKEN ready"
```

#### 4) Create a forecast and retrieve it

**Note:** this is a forecast API. The `POST /weather` endpoint validates that the date is **today or later** (UTC). To avoid edge cases, we use tomorrow’s date.

```bash
FORECAST_DATE="$(date -u -d '+1 day' +'%Y-%m-%d')"
FORECAST_DT="${FORECAST_DATE}T00:00:00Z"

# Create forecast (write)
curl -i -X POST \
  -H "Authorization: Bearer $READWRITE_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
        \"date\": \"${FORECAST_DT}\",
        \"temperatureC\": 12,
        \"summary\": \"Sunny\"
      }" \
  http://localhost:8080/weather

# Get forecast (read)
curl -i \
  -H "Authorization: Bearer $READWRITE_TOKEN" \
  "http://localhost:8080/weather/${FORECAST_DATE}"
```

(Optional) Delete forecast (admin):

```bash
curl -i -X DELETE \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  "http://localhost:8080/weather/${FORECAST_DATE}"
```
