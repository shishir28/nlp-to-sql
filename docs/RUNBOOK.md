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

## Quick Start (Step-by-Step)

### Step 1: Start Database

```bash
cd db
docker compose up -d
```

Wait for MySQL to become healthy (~20 seconds):
```bash
docker compose ps
# Wait until mysql shows "healthy" status
```

Verify MySQL is accessible:
```bash
docker exec -it mysql_db mysql -uapp_user -papp_pass_secure -e "SELECT 1"
```

### Step 2: Run Database Migrations

From the `db/` directory:
```bash
flyway -configFiles=flyway.conf migrate
```

Expected output:
```
Successfully applied 2 migrations
  V1__init_schema.sql
  V2__reference_data.sql
```

Verify schema:
```bash
docker exec -it mysql_db mysql -uapp_user -papp_pass_secure property_analytics \
  -e "SHOW TABLES"
```

### Step 3: Seed Database with Test Data

```bash
cd ../backend/src/SeedRunner
dotnet run
```

Expected output:
```
🌱 Property Analytics Database Seeder
=====================================

Testing database connection...
✅ Connected to MySQL

Seed Configuration:
  Customers: 10
  Properties per customer: 20
  Tenancies per customer: 25
  Maintenance jobs per customer: 400

🗑️  Cleaning existing data...
👥 Seeding customers...
  ✅ Seeded 10 customers
🏘️  Seeding properties, owners, tenants, vendors...
...
✅ Seeding complete!
```

### Step 4: Start Python Agent Service

```bash
cd ../../../agents/nl2sql-service
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

Expected output:
```
INFO:     Started server process
INFO:     Waiting for application startup.
INFO:     Application startup complete.
INFO:     Uvicorn running on http://0.0.0.0:8000
```

Test agent health:
```bash
curl http://localhost:8000/health
# Should return: {"status":"healthy","service":"nl2sql-agent","version":"1.0.0"}
```

### Step 5: Start .NET API

Open a new terminal:
```bash
cd backend/src/Api
dotnet run
```

Expected output:
```
🚀 API Server starting on http://localhost:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

Test API health:
```bash
curl http://localhost:5000/api/query/health
# Should return: {"status":"healthy","service":"nl2sql-api","timestamp":"..."}
```

### Step 6: Start Angular Frontend

Open a new terminal:
```bash
cd frontend
npm install  # First time only
npm start
```

Expected output:
```
✔ Browser application bundle generation complete.
Initial Chunk Files   | Names         |  Raw Size
polyfills.js          | polyfills     |  83.60 kB | 
main.js               | main          |  22.31 kB | 
styles.css            | styles        |   5.25 kB | 

** Angular Live Development Server is listening on localhost:4200 **
```

### Step 7: Smoke Test

1. Open browser to http://localhost:4200
2. Enter question: **"Show active tenancies ending in next 60 days"**
3. Click **"Run Query"**
4. Verify results table appears with data

Expected behavior:
- Page loads with purple gradient background
- Example buttons work
- Query executes in < 3 seconds
- Results table shows columns: TenancyId, TenantName, PropertyAddress, LeaseStartDate, LeaseEndDate, RentAmount, RentFrequency, DaysUntilExpiry

## Service URLs

Once running, access:

| Service | URL | Purpose |
|---------|-----|---------|
| Angular UI | http://localhost:4200 | Main user interface |
| .NET API | http://localhost:5000 | Trust boundary / SQL execution |
| Python Agent | http://localhost:8000 | LangGraph SQL generation |
| MySQL | localhost:3306 | Database |
| Adminer | http://localhost:8080 | Database UI (login: app_user / app_pass_secure) |
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

### Agent returns clarification instead of SQL
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
# Stop frontend (Ctrl+C in terminal)
# Stop API (Ctrl+C in terminal)
# Stop Python agent (Ctrl+C in terminal)

# Stop database
cd db
docker compose down

# Stop and remove all data
docker compose down -v
```

## Resetting Everything

Complete clean slate:
```bash
# Stop all services
docker compose down -v

# Clean .NET build artifacts
cd backend/src
dotnet clean
rm -rf **/bin **/obj

# Clean Python venv
cd ../../agents/nl2sql-service
rm -rf .venv

# Clean frontend
cd ../../frontend
rm -rf node_modules dist

# Now follow Quick Start from Step 1
```

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
