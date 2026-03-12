# 🚀 NL2SQL Property Analytics - Runbook

## Prerequisites

Ensure you have the following installed:

- **Node.js**: v20.19.0 or higher
- **.NET SDK**: 8.0 or higher
- **Python**: 3.11 or higher
- **Docker**: Latest version with Docker Compose
- **Flyway CLI**: 10.x or higher

Check installations:
```bash
node --version
dotnet --version
python --version
docker --version
flyway --version
```

## Quick Start

### One-Command Start

```bash
docker-compose up -d
```

Wait ~30 seconds for all services to become healthy, then open **http://localhost:4200**.

Check all services are healthy:
```bash
docker-compose ps
```

All containers should show `healthy` or `running`. The `nlp2sql-seeder` container will exit with code 0 after completing migrations and seeding — this is expected.

### Smoke Test

```bash
# Agent health
curl http://localhost:8000/health

# API health
curl http://localhost:5000/api/query/health

# Run a query
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"question":"Show active tenancies","customerId":"1","role":"PropertyManager"}'
```

## Service URLs

| Service | URL | Purpose |
|---------|-----|---------|
| Angular UI | http://localhost:4200 | Main UI (query tab, dashboard, analytics, reports) |
| .NET API | http://localhost:5000 | Trust boundary / SQL execution |
| Python Agent | http://localhost:8000 | LangGraph SQL generation |
| MySQL | localhost:**3307** | Database (user: app_user / pass: app_pass_secure) |
| Redis | localhost:6379 | Conversation memory store |
| Adminer | http://localhost:8080 | Database UI (app_user / app_pass_secure / property_analytics) |
| Swagger | http://localhost:5000/swagger | API documentation |

## Troubleshooting

### MySQL won't start
```bash
# Check logs
docker compose logs mysql

# Common fix: Remove volume and restart
docker compose down -v
docker compose up -d
```

### Flyway migration fails
```bash
# Check connection
flyway -configFiles=flyway.conf info

# Reset database (WARNING: deletes all data)
flyway -configFiles=flyway.conf clean
flyway -configFiles=flyway.conf migrate
```

### Python agent fails to start
```bash
# Check Python version (must be 3.11+)
python --version

# Reinstall dependencies
pip install --upgrade -r requirements.txt
```

### .NET API compilation errors
```bash
# Restore packages
dotnet restore

# Clean build
dotnet clean
dotnet build
```

### Frontend npm errors
```bash
# Clear cache
npm cache clean --force

# Delete node_modules and reinstall
rm -rf node_modules
npm install
```

### Agent returns clarification instead of results
**Symptom**: Every query returns "I need more information..."

**Cause**: Python service keyword matching too strict

**Fix**: Check `agents/nl2sql-service/app/graph.py` route_domain() function - add more keywords for your use case

### SQL Firewall blocks valid query
**Symptom**: API returns "Query blocked" with violations

**Check logs**: Look for firewall rule hits in API console

**Fix**: Update `db/policy/schema-policy.json` to add missing tables/functions to allowlists

### CORS errors in browser console
**Symptom**: `Access to XMLHttpRequest has been blocked by CORS policy`

**Fix**: Verify API Program.cs has correct CORS origin (http://localhost:4200)

## Stopping Services

```bash
# Stop all containers
docker-compose down

# Stop and remove all data (full reset)
docker-compose down -v
```

## Full Reset

```bash
docker-compose down -v
docker-compose up -d
```

This removes all volumes and restarts from scratch. Migrations and seed data reload automatically.

## Production Deployment Notes

This is a **development setup**. For production:

1. **Replace dev connection strings** in appsettings.json
2. **Add proper JWT authentication** (remove dev fallback customerId="1")
3. **Use HTTPS** for all services
4. **Configure CORS** to allow only production frontend URL
5. **Add rate limiting** to API endpoints
6. **Enable SQL query result caching** in executor
7. **Add comprehensive logging/monitoring** (Application Insights, Sentry)
8. **Use production-grade LLM** in Python agent (replace templates with real LangChain chains)
9. **Add database connection pooling** with retry policies
10. **Configure firewall with stricter limits** (lower LIMIT defaults, shorter timeouts)

## Support

For issues:
1. Check logs in terminal windows
2. Verify all services are running (health endpoints)
3. Check database has seeded data: `docker exec -it mysql_db mysql -uapp_user -papp_pass_secure property_analytics -e "SELECT COUNT(*) FROM Properties"`
4. Review [ARCHITECTURE.md](ARCHITECTURE.md) for design decisions
