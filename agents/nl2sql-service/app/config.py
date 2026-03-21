"""
Configuration for LLM models and agents
"""
import os
from typing import Optional
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # LLM Configuration
    openai_api_key: Optional[str] = None
    openai_model: str = "gpt-4o-mini"  # or gpt-4, gpt-3.5-turbo
    openai_temperature: float = 0.1  # Low temperature for deterministic SQL
    
    # Alternative: Azure OpenAI
    azure_openai_api_key: Optional[str] = None
    azure_openai_endpoint: Optional[str] = None
    azure_openai_deployment: Optional[str] = None
    
    # Alternative: Local model (Ollama)
    use_local_llm: bool = False
    local_llm_base_url: str = "http://localhost:11434"
    local_llm_model: str = "llama3.2"
    
    # Database Configuration (for schema introspection)
    db_host: str = "localhost"
    db_port: int = 3306
    db_name: str = "propertydb"
    db_user: str = "app"
    db_password: str = ""
    schema_cache_ttl: int = 300  # seconds to cache INFORMATION_SCHEMA results

    # Agent Configuration
    max_retries: int = 2
    agent_timeout: int = 30
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
