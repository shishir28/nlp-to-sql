"""
Conversation store for multi-turn NL2SQL interactions.
Persists turn history keyed by conversation_id using Redis (with in-memory fallback),
following the same pattern as QueryCache in llm_graph.py.
"""
import json
import time
import logging
from typing import Dict, List, Optional

logger = logging.getLogger(__name__)

# Max turns to keep per conversation (prevents unbounded context growth)
MAX_HISTORY_TURNS = 10


class ConversationStore:
    """
    Stores conversation turns so agents can reference prior questions and clarifications.

    Each turn is stored as:
        {"question": str, "clarification_prompt": str | None, "sql_candidate": str, "timestamp": float}

    Redis key: nl2sql:conv:{conversation_id}
    """

    def __init__(self, ttl: int = 3600):
        self.ttl = ttl
        self.local_store: Dict[str, List[Dict]] = {}
        self.redis = None

        try:
            import redis
            redis_url = "redis://redis:6379"
            self.redis = redis.from_url(redis_url, decode_responses=True)
            self.redis.ping()
            logger.info(f"[ConversationStore] Connected to Redis at {redis_url}")
        except Exception as e:
            logger.warning(f"[ConversationStore] Redis unavailable, using in-memory only: {e}")

    def _redis_key(self, conversation_id: str) -> str:
        return f"nl2sql:conv:{conversation_id}"

    def get_history(self, conversation_id: str) -> List[Dict]:
        """Return all turns for a conversation, most-recent last."""
        key = self._redis_key(conversation_id)

        if self.redis:
            try:
                raw = self.redis.get(key)
                if raw:
                    logger.debug(f"[ConversationStore] History loaded from Redis: {conversation_id}")
                    return json.loads(raw)
            except Exception as e:
                logger.warning(f"[ConversationStore] Redis get error: {e}")

        entry = self.local_store.get(conversation_id)
        if entry and time.time() - entry[-1].get("timestamp", 0) < self.ttl:
            logger.debug(f"[ConversationStore] History loaded from local: {conversation_id}")
            return entry

        return []

    def append_turn(self, conversation_id: str, question: str,
                    clarification_prompt: Optional[str], sql_candidate: str) -> None:
        """Append a completed turn to the conversation history."""
        history = self.get_history(conversation_id)

        turn = {
            "question": question,
            "clarification_prompt": clarification_prompt,
            "sql_candidate": sql_candidate,
            "timestamp": time.time(),
        }
        history.append(turn)

        # Keep only the most recent N turns
        if len(history) > MAX_HISTORY_TURNS:
            history = history[-MAX_HISTORY_TURNS:]

        key = self._redis_key(conversation_id)
        serialised = json.dumps(history)

        if self.redis:
            try:
                self.redis.setex(key, self.ttl, serialised)
                logger.debug(f"[ConversationStore] Turn saved to Redis: {conversation_id}")
            except Exception as e:
                logger.warning(f"[ConversationStore] Redis set error: {e}")

        self.local_store[conversation_id] = history
        logger.info(f"[ConversationStore] Appended turn #{len(history)} for conversation {conversation_id}")

    def format_history_for_prompt(self, history: List[Dict]) -> str:
        """
        Render conversation history as a concise text block for LLM injection.

        Example output:
            Turn 1: User asked 'show active tenancies'. SQL generated successfully.
            Turn 2: User asked 'now filter by rent below 500'. SQL generated successfully.
        """
        if not history:
            return ""

        lines = []
        for i, turn in enumerate(history, start=1):
            q = turn.get("question", "")
            clarification = turn.get("clarification_prompt")
            if clarification:
                lines.append(f"Turn {i}: User asked '{q}'. Clarification was requested: '{clarification}'.")
            else:
                lines.append(f"Turn {i}: User asked '{q}'. SQL generated successfully.")

        return "\n".join(lines)


# Global singleton
_conversation_store: Optional[ConversationStore] = None


def get_conversation_store() -> ConversationStore:
    global _conversation_store
    if _conversation_store is None:
        _conversation_store = ConversationStore(ttl=3600)
    return _conversation_store
