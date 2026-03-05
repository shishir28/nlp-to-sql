# NLP-to-SQL Docker Deployment Guide

This guide explains how to deploy the entire NLP-to-SQL system using Docker Compose.

## Architecture Overview

The Docker deployment consists of 6 services:

1. **MySQL 8.4** - Database server (port 3306)
2. **Redis 7** - Conversation store + query result cache (port 6379)
3. **Adminer** - Web-based database management tool (port 8080)
4. **Python Agent** - LangGraph NL-to-SQL service with SSE streaming (port 8000)
5. **ASP.NET Core API** - Backend API with security enforcement (port 5000)
6. **Angular Frontend** - User interface with nginx (port 4200)

All services are connected via a Docker bridge network (`nlp2sql-network`) and use container-to-container communication.

## Prerequisites

- Docker Engine 20.10+ or Docker Desktop
- Docker Compose v2.0+
- At least 4GB RAM available for Docker
- Ports available: 3306, 5000, 8000, 4200, 8080

## Quick Start

### 1. Build and Start All Services

```bash
docker compose up --build -d
```

This will:
- Build Docker images for the Python agent, .NET API, and Angular frontend
- Start all 5 services in the correct order (with health checks)
- Create a Docker network for inter-service communication

**First build takes 5-10 minutes** depending on your machine.

### 2. Check Service Status

```bash
docker compose ps
```

You should see all 6 services running and healthy:

```
NAME                IMAGE                    STATUS
nlp2sql-mysql       mysql:8.4                Up (healthy)
nlp2sql-redis       redis:7-alpine           Up (healthy)
nlp2sql-adminer     adminer:4                Up
nlp2sql-agent       db_agent                 Up (healthy)
nlp2sql-api         db_api                   Up (healthy)
nlp2sql-frontend    db_frontend              Up
```

### 3. Run Database Migrations

The database schema needs to be created after MySQL starts:

```bash
docker exec -i nlp2sql-mysql mysql -uapp_user -papp_pass_secure property_analytics < db/migrations/V1__init_schema.sql
```

Or use Flyway:

```bash
flyway -configFiles=db/flyway.conf migrate
```

### 4. Access the Application

- **Frontend UI**: http://localhost:4200
- **API**: http://localhost:5000/api/query
- **Python Agent**: http://localhost:8000 (internal, no direct access needed)
- **Adminer**: http://localhost:8080
  - System: MySQL
  - Server: mysql
  - Username: app_user
  - Password: app_pass_secure
  - Database: property_analytics

## Docker Compose Configuration

### Service Dependencies

```
mysql (healthy) + redis (healthy) → adminer + agent (healthy) → api (healthy) → frontend
```

Health checks ensure services start in the correct order:
- MySQL must be healthy before agent and API start
- Agent must be healthy before API starts
- API must be healthy before frontend starts

### Environment Variables

#### Agent Service

```yaml
PYTHONUNBUFFERED=1
REDIS_URL=redis://redis:6379          # Conversation store + query cache
# Schema introspection (queries INFORMATION_SCHEMA for real FK relationships)
DB_HOST=mysql
DB_PORT=3306
DB_NAME=property_analytics
DB_USER=app_user
DB_PASSWORD=app_pass_secure
SCHEMA_CACHE_TTL=300                  # Seconds to cache FK map
# LLM mode (optional)
USE_LLM_AGENTS=false                  # Set to true to enable LLM agents
OPENAI_API_KEY=sk-...                 # Required if USE_LLM_AGENTS=true
```

Note: DB_* variables allow the agent's `InformationSchemaClient` to discover real foreign-key relationships from `INFORMATION_SCHEMA.KEY_COLUMN_USAGE`. If the DB is unreachable, the agent falls back to fuzzy table-name matching automatically.

#### API Service

```yaml
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5000
ConnectionStrings__MySql=Server=mysql;Port=3306;Database=property_analytics;...
AgentService__BaseUrl=http://agent:8000
```

Note: `Server=mysql` uses the container name for networking.

### Networking

All services use the `nlp2sql-network` bridge network. This allows:
- Frontend nginx to proxy `/api/*` to `http://nlp2sql-api:5000`
- .NET API to call `http://agent:8000` for NL-to-SQL translation
- .NET API to connect to `Server=mysql` for database queries

## Development Workflow

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
docker compose logs -f agent
docker compose logs -f frontend
```

### Restart a Service

```bash
docker compose restart api
```

### Rebuild After Code Changes

```bash
# Rebuild all services
docker compose up --build -d

# Rebuild specific service
docker compose build api
docker compose up -d api
```

### Stop All Services

```bash
docker compose down
```

### Stop and Remove Volumes (WARNING: deletes database data)

```bash
docker compose down -v
```

## Troubleshooting

### Port Already in Use

If you get "port is already allocated" errors:

```bash
# Check what's using the port
lsof -i :5000

# Kill the process or change the port in docker-compose.yml
```

### API Can't Connect to MySQL

Check if MySQL is healthy:

```bash
docker compose ps mysql
docker compose logs mysql
```

Ensure the health check passes:

```bash
docker exec nlp2sql-mysql mysqladmin ping -h localhost -uroot -prootpass123
```

### API Can't Connect to Agent

Check if the agent is healthy:

```bash
docker compose ps agent
docker compose logs agent
curl http://localhost:8000/health
```

### Frontend Shows API Errors

1. Check if API is healthy:

```bash
docker compose ps api
docker compose logs api
curl http://localhost:5000/api/query/health
```

2. Check nginx proxy configuration:

```bash
docker exec nlp2sql-frontend cat /etc/nginx/conf.d/default.conf | grep proxy_pass
```

Should show: `proxy_pass http://nlp2sql-api:5000;`

### Database Connection Refused

The API uses `Server=mysql` (container name) not `localhost`. Verify:

```bash
docker compose exec api printenv | grep ConnectionStrings
```

### Build Failures

Clear Docker build cache and rebuild:

```bash
docker compose down
docker builder prune -a
docker compose build --no-cache
docker compose up -d
```

## Seed Data

To populate the database with sample data:

### Option 1: Use Seed Generator (External)

```bash
cd ../backend/tools/SeedGenerator
dotnet run
```

Configure connection string in appsettings.json to point to `localhost:3306`.

### Option 2: SQL Import

Export data from another environment and import:

```bash
docker exec -i nlp2sql-mysql mysql -uapp_user -papp_pass_secure property_analytics < seed_data.sql
```

## Production Considerations

### Security

1. **Change default passwords** in docker-compose.yml:
   - MYSQL_ROOT_PASSWORD
   - MYSQL_PASSWORD
   - Update ConnectionStrings__MySql accordingly

2. **Use Docker secrets** for sensitive data:

```yaml
secrets:
  db_password:
    file: ./secrets/db_password.txt

services:
  api:
    secrets:
      - db_password
    environment:
      - ConnectionStrings__MySql=Server=mysql;...;Password_File=/run/secrets/db_password
```

3. **Disable exposed ports** for internal services:

Edit docker-compose.yml and remove `ports:` sections for agent and API (only frontend should be exposed).

4. **Use SSL/TLS**:
   - Configure nginx with SSL certificates
   - Use `https://` in frontend environment.prod.ts

### Performance

1. **Resource limits**:

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M
```

2. **Restart policies**:

Already configured with `restart: unless-stopped`

3. **Persistent volumes**:

MySQL data is persisted in `mysql_data` volume. Backup regularly:

```bash
docker exec nlp2sql-mysql mysqldump -uroot -prootpass123 property_analytics > backup.sql
```

### Monitoring

Add health check endpoints to your monitoring system:

- API: `http://your-domain:5000/api/query/health`
- Agent: `http://your-domain:8000/health`
- MySQL: Use mysqladmin ping or connect via monitoring tool

### Scaling

For production load, consider:

1. **Multiple agent replicas**:

```yaml
agent:
  deploy:
    replicas: 3
```

2. **Load balancer** in front of API

3. **Database replicas** for read-only queries

4. **Redis cache** for query results

## File Structure

```
nlp-to-sql/
├── docker-compose.yml           # Orchestration file (root level)
├── db/
│   ├── migrations/
│   │   └── V1__init_schema.sql
│   ├── policy/
│   │   └── schema-policy.json
│   └── flyway.conf
├── backend/
│   ├── Dockerfile               # .NET API multi-stage build
│   ├── .dockerignore
│   └── src/
├── agents/
│   ├── Dockerfile               # Python service
│   └── nl2sql-service/
└── frontend/
    ├── Dockerfile               # Angular + nginx multi-stage build
    ├── nginx.conf               # Reverse proxy config
    ├── .dockerignore
    └── src/
```

## Build Context

All services use the parent directory (`..`) as build context to share the policy file:

```yaml
services:
  api:
    build:
      context: .               # nlp-to-sql/ (repo root)
      dockerfile: backend/Dockerfile
```

This allows:
- Backend Dockerfile to copy `db/policy/schema-policy.json`
- Frontend Dockerfile to copy `frontend/nginx.conf`
- Consistent builds across services from root directory
## Multi-Stage Builds

All application services use multi-stage builds for optimization:

### Backend (2 stages)
1. **Build stage**: Uses `mcr.microsoft.com/dotnet/sdk:8.0` to compile C# code
2. **Runtime stage**: Uses `mcr.microsoft.com/dotnet/aspnet:8.0` (smaller image)

### Frontend (2 stages)
1. **Build stage**: Uses `node:20-alpine` to build Angular
2. **Runtime stage**: Uses `nginx:alpine` to serve static files

### Agent (1 stage)
- Uses `python:3.11-slim` (already minimal)

**Result**: Smaller images, faster deployments, no unnecessary build tools in production.

## Next Steps

1. **Test the full stack**: Run a query through the UI
2. **Seed data**: Populate with Australian property records
3. **Monitor logs**: Watch for any errors during query execution
4. **Performance testing**: Run multiple concurrent queries
5. **Security audit**: Review exposed ports and credentials

## Support

For issues or questions:
- Check logs: `docker compose logs -f`
- Review RUNBOOK.md for detailed setup
- Review ARCHITECTURE.md for system design

---

**Last Updated**: 2024
**Docker Compose Version**: 3.9
**Supported Platforms**: Linux, macOS, Windows (WSL2)
