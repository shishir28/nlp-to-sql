# LLM-Powered Multi-Agent SQL Generation

This system supports two modes:
1. **Template-based** (default): Fast, deterministic, zero-cost
2. **LLM-powered**: Flexible, handles complex queries, requires API key

## Architecture

### LLM Multi-Agent Pipeline

```
User Question
     ↓
[Agent 1: Domain Classifier]
     ↓ (Domain + Tables)
[Agent 2: Schema Analyzer]
     ↓ (Query Plan)
[Agent 3: SQL Generator]
     ↓ (SQL Candidate)
[Agent 4: SQL Validator]
     ↓ (Validated SQL)
(.NET firewall + MySQL execution)
     ↓
NL Results + Explanation (UI)
```

### Agent Responsibilities

1. **Domain Classifier Agent**
   - Uses LLM to classify question into business domain
   - Identifies relevant tables using semantic understanding
   - Outputs: domain, confidence, scoped_tables

2. **Schema Analyzer Agent**
   - Creates detailed query plan
   - Identifies JOINs, filters, aggregations
   - Handles temporal logic (past/future)
   - Outputs: query_plan, schema_description

3. **SQL Generator Agent**
   - Generates SQL from query plan
   - Follows MySQL 8.4 syntax
   - Outputs: sql_candidate

4. **SQL Validator Agent**
   - Reviews generated SQL for correctness
   - Checks logic, syntax, performance
   - May revise SQL if issues found
   - Outputs: confidence, validation_notes, revised_sql

## Execution Constraints

- SQL is generated and validated internally; the frontend does not render SQL text.
- Execution is MySQL-only (`dialect: mysql8`) end-to-end.
- Agent is constrained by policy inputs from the API:
  - `allowed_tables`
  - `allowed_functions`
  - `default_limit` / `max_limit`
  - `tenant_column`

## Quick Start

### Option 1: Template Mode (No LLM - Default)

```bash
# Already running! Nothing to configure
docker-compose up -d
```

### Option 2: OpenAI LLM Mode

1. Copy environment template:
```bash
cp agents/nl2sql-service/.env.example agents/nl2sql-service/.env
```

2. Edit `.env` and add your OpenAI API key:
```env
USE_LLM_AGENTS=true
OPENAI_API_KEY=sk-your-key-here
OPENAI_MODEL=gpt-4o-mini  # or gpt-4, gpt-3.5-turbo
```

3. Set environment variable and restart:
```bash
export USE_LLM_AGENTS=true
export OPENAI_API_KEY=sk-your-key-here
docker-compose up -d agent
```

### Option 3: Azure OpenAI

```env
USE_LLM_AGENTS=true
AZURE_OPENAI_API_KEY=your-azure-key
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_DEPLOYMENT=your-deployment-name
```

### Option 4: Local LLM (Ollama)

1. Install Ollama: https://ollama.ai
2. Pull a model:
```bash
ollama pull llama3.1:8b
```

3. Configure:
```env
USE_LLM_AGENTS=true
USE_LOCAL_LLM=true
LOCAL_LLM_MODEL=llama3.1:8b
```

## Testing LLM Mode

```bash
# Check health
curl http://localhost:8000/health

# Should show: "mode": "LLM-powered multi-agent"

# Test query
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Show me properties in Sydney with tenancies ending in the next 30 days where rent is overdue",
    "customerId": 1
  }'
```

## When to Use Each Mode

### Template Mode (Default)
✅ **Use when:**
- Queries fit common patterns
- Zero latency required
- No API costs desired
- Predictability is critical

❌ **Limitations:**
- Can't handle novel query patterns
- Requires code changes for new domains
- Limited to predefined templates

### LLM Mode
✅ **Use when:**
- Handling complex, novel queries
- Need semantic understanding
- Want natural language flexibility  
- Can afford API latency/costs

❌ **Considerations:**
- API costs (~$0.001 per query with gpt-4o-mini)
- Higher latency (1-3 seconds)
- Requires API key management
- Potential variability in output

## Configuration Reference

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `USE_LLM_AGENTS` | `false` | Enable LLM-powered agents |
| `OPENAI_API_KEY` | - | OpenAI API key |
| `OPENAI_MODEL` | `gpt-4o-mini` | Model to use |
| `OPENAI_TEMPERATURE` | `0.1` | Temperature (0.0-1.0) |
| `USE_LOCAL_LLM` | `false` | Use local Ollama instead |
| `LOCAL_LLM_MODEL` | `llama3.1:8b` | Local model name |

## Cost Estimation

### OpenAI Pricing (as of 2024)
- **gpt-4o-mini**: ~$0.001 per query (recommended)
- **gpt-4**: ~$0.03 per query
- **gpt-3.5-turbo**: ~$0.002 per query

Example: 1000 queries/day with gpt-4o-mini = $30/month

### Local LLM
- **Cost**: $0 (runs on your hardware)
- **Requirements**: 8GB+ RAM for llama3.1:8b
- **Performance**: Slower than cloud models

## Monitoring

Check agent health and mode:
```bash
curl http://localhost:8000/health | jq
```

Output:
```json
{
  "status": "healthy",
  "service": "nl2sql-agent",
  "version": "2.0.0",
  "mode": "LLM-powered multi-agent",
  "llm_agents": "enabled (gpt-4o-mini)"
}
```

## Troubleshooting

### LLM Mode Not Working
1. Check API key is set: `echo $OPENAI_API_KEY`
2. Check logs: `docker-compose logs agent`
3. Verify health endpoint shows LLM enabled

### Fallback to Templates
If LLM fails (API error, timeout), system automatically falls back to templates with warning in logs.

## Security Notes

1. **Never commit .env files** with API keys
2. API keys are **not stored** in database
3. Use environment variables only
4. Consider using secrets management in production
5. Tenant filtering still enforced by .NET firewall
6. SQL firewall enforces allowlists, join depth, and limit capping before execution

## Next Steps

- [ ] Implement RAG for schema documentation
- [ ] Add query cache to reduce API calls
- [ ] Implement batch processing for multiple queries
- [ ] Add custom fine-tuned models for domain-specific SQL
