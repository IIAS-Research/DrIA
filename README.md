# DrIA

DrIA helps physicians create AI models from their data. Its guided workflow lets you upload a CSV file, explore the data, train models, understand the results and try predictions. Computation is handled by the IAML web service.

## Run with Docker

Requirements: Docker with Docker Compose.

```bash
cp .env.example .env
```

In `.env`, set `POSTGRES_PASSWORD`, `JWT_SECRET` and `DRIA_ADMIN_PASSWORD`. The JWT secret must contain at least 32 characters. The administrator password must contain at least 6 characters, including an uppercase letter, a lowercase letter, a digit and a special character. You can also configure the initial account's email address and the application port. Existing accounts keep their credentials.

```bash
docker compose up -d --build
```

Open [http://localhost:8080](http://localhost:8080) and sign in with the configured credentials. Compose starts DrIA, PostgreSQL and the [IAML web service](https://hub.docker.com/r/rudymerieux/iaml-service), with persistent storage for files and results. The IAML image is currently available for x86-64.

To stop the application and keep its data:

```bash
docker compose down
```

## Run locally

Requirements: .NET 8 SDK, Node.js 18 and PostgreSQL. A PostgreSQL database must be available, and the IAML web service must be running at `http://localhost:8000`.

From the project root, configure and start the server, replacing the values in angle brackets:

```bash
cd Server
dotnet user-secrets set "ConnectionString" "Host=localhost;Database=dr_ia_dev;Username=<username>;Password=<password>"
dotnet user-secrets set "JwtPrivateKey" "<random secret of at least 32 characters>"
dotnet user-secrets set "PathMainStorage" "$PWD/Storage"
dotnet user-secrets set "Iaml:BaseUrl" "http://localhost:8000"
dotnet run --launch-profile http
```

The server is available at `http://localhost:3000` and initializes the database on startup.

In a second terminal, start the web interface from the project root:

```bash
cd client
npm ci
npm run dev
```

Open [http://localhost:9000](http://localhost:9000).

## Local account

An administrator account is created automatically when running in development:

- Email: `admin@dria.local`
- Password: `DrIA-Dev123!`

If this account already exists, its credentials are preserved.
