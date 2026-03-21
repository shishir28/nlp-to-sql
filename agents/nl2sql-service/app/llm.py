"""
LLM initialization and utilities
"""
from langchain_openai import ChatOpenAI
from langchain_core.language_models import BaseChatModel
from app.config import settings


def get_llm() -> BaseChatModel:
    """
    Initialize and return the configured LLM.
    Supports OpenAI and Azure OpenAI. Local Ollama is disabled.
    """

    # Option 1: Azure OpenAI
    if settings.azure_openai_api_key:
        return ChatOpenAI(
            api_key=settings.azure_openai_api_key,
            azure_endpoint=settings.azure_openai_endpoint,
            azure_deployment=settings.azure_openai_deployment,
            temperature=settings.openai_temperature,
            model_name=settings.openai_model,
            request_timeout=25,
        )

    # Option 2: Standard OpenAI
    if settings.openai_api_key:
        return ChatOpenAI(
            api_key=settings.openai_api_key,
            model=settings.openai_model,
            temperature=settings.openai_temperature,
            request_timeout=25,  # hard per-call cap; avoids hanging on slow responses
        )

    # No key configured — template mode will handle it
    print("WARNING: No LLM API key configured. LLM agents will be skipped.")
    return None


# Global LLM instance
_llm_instance = None

def get_llm_instance() -> BaseChatModel:
    """Get singleton LLM instance"""
    global _llm_instance
    if _llm_instance is None:
        _llm_instance = get_llm()
    return _llm_instance
