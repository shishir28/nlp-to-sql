"""
LLM initialization and utilities
"""
from typing import Union
from langchain_openai import ChatOpenAI
from langchain_core.language_models import BaseChatModel
from app.config import settings


def get_llm() -> BaseChatModel:
    """
    Initialize and return the configured LLM.
    Supports OpenAI, Azure OpenAI, and local models.
    """
    
    # Option 1: Use local model (Ollama)
    if settings.use_local_llm:
        try:
            from langchain_ollama import ChatOllama
        except ImportError:
            from langchain_community.chat_models import ChatOllama
        return ChatOllama(
            base_url=settings.local_llm_base_url,
            model=settings.local_llm_model,
            temperature=settings.openai_temperature,
            timeout=60.0,  # Increase timeout for local models
            num_predict=512  # Limit response length for faster generation
        )
    
    # Option 2: Azure OpenAI
    if settings.azure_openai_api_key:
        return ChatOpenAI(
            api_key=settings.azure_openai_api_key,
            azure_endpoint=settings.azure_openai_endpoint,
            azure_deployment=settings.azure_openai_deployment,
            temperature=settings.openai_temperature,
            model_name=settings.openai_model
        )
    
    # Option 3: Standard OpenAI (default)
    if settings.openai_api_key:
        return ChatOpenAI(
            api_key=settings.openai_api_key,
            model=settings.openai_model,
            temperature=settings.openai_temperature
        )
    
    # Fallback: Use mock LLM for testing without API key
    print("WARNING: No LLM API key configured. Using template-based fallback.")
    return None


# Global LLM instance
_llm_instance = None

def get_llm_instance() -> BaseChatModel:
    """Get singleton LLM instance"""
    global _llm_instance
    if _llm_instance is None:
        _llm_instance = get_llm()
    return _llm_instance
