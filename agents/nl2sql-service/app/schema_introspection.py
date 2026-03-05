"""
Schema introspection service for discovering database tables and columns
using MySQL INFORMATION_SCHEMA when manual mappings don't exist.
"""
import json
import time
import logging
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from difflib import SequenceMatcher

logger = logging.getLogger(__name__)


class InformationSchemaClient:
    """
    Queries MySQL INFORMATION_SCHEMA to discover real FK relationships and table metadata.
    Results are cached with a configurable TTL to avoid hammering the DB.
    """

    def __init__(self, host: str, port: int, db: str, user: str, password: str, ttl: int = 300):
        self.dsn = dict(host=host, port=port, database=db, user=user, password=password)
        self.ttl = ttl
        self._fk_cache: Optional[Dict[str, List[str]]] = None
        self._fk_cache_at: float = 0.0

    def _connect(self):
        """Return a new mysql.connector connection (caller must close)."""
        import mysql.connector
        return mysql.connector.connect(**self.dsn)

    def get_related_tables(self, primary_table: str, allowed_tables: List[str]) -> List[str]:
        """
        Return tables that have a FK relationship with primary_table,
        filtered to allowed_tables. Falls back to empty list on error.
        """
        fk_map = self._load_fk_map()
        related = fk_map.get(primary_table, [])
        return [t for t in related if t in allowed_tables and t != primary_table]

    def _load_fk_map(self) -> Dict[str, List[str]]:
        """Load (or return cached) FK adjacency map: table → [tables it references or is referenced by]."""
        now = time.monotonic()
        if self._fk_cache is not None and (now - self._fk_cache_at) < self.ttl:
            return self._fk_cache

        try:
            conn = self._connect()
            cursor = conn.cursor()
            cursor.execute("""
                SELECT TABLE_NAME, REFERENCED_TABLE_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE REFERENCED_TABLE_NAME IS NOT NULL
                  AND TABLE_SCHEMA = DATABASE()
            """)
            rows = cursor.fetchall()
            cursor.close()
            conn.close()

            fk_map: Dict[str, List[str]] = {}
            for child, parent in rows:
                fk_map.setdefault(parent, [])
                if child not in fk_map[parent]:
                    fk_map[parent].append(child)
                fk_map.setdefault(child, [])
                if parent not in fk_map[child]:
                    fk_map[child].append(parent)

            self._fk_cache = fk_map
            self._fk_cache_at = now
            logger.info(f"[InformationSchemaClient] Loaded FK map: {len(fk_map)} tables")
            return fk_map

        except Exception as e:
            logger.warning(f"[InformationSchemaClient] INFORMATION_SCHEMA query failed: {e}")
            return self._fk_cache or {}


class SchemaIntrospector:
    """Discovers relevant tables and columns from database schema"""
    
    def __init__(self, mapping_file: str = "domain-mapping.json"):
        self.mapping_file = Path(__file__).parent.parent / mapping_file
        self.domain_mappings = self._load_mappings()
        self._info_schema: Optional[InformationSchemaClient] = self._init_info_schema()

    def _init_info_schema(self) -> Optional[InformationSchemaClient]:
        try:
            from app.config import settings
            client = InformationSchemaClient(
                host=settings.db_host,
                port=settings.db_port,
                db=settings.db_name,
                user=settings.db_user,
                password=settings.db_password,
                ttl=settings.schema_cache_ttl,
            )
            logger.info("[SchemaIntrospector] InformationSchemaClient initialised")
            return client
        except Exception as e:
            logger.warning(f"[SchemaIntrospector] Could not initialise InformationSchemaClient: {e}")
            return None
        
    def _load_mappings(self) -> Dict:
        """Load manual domain mappings from JSON"""
        try:
            with open(self.mapping_file, 'r') as f:
                return json.load(f)
        except FileNotFoundError:
            return {"domains": {}, "schema_introspection": {"enabled": True}}
    
    def find_domain_by_keywords(self, question: str) -> Optional[Dict]:
        """
        Find domain mapping from JSON based on keywords.
        Returns domain info if found, None otherwise.
        """
        question_lower = question.lower()
        
        for domain_name, domain_info in self.domain_mappings.get("domains", {}).items():
            keywords = domain_info.get("keywords", [])
            if any(keyword in question_lower for keyword in keywords):
                return {
                    "domain": domain_name,
                    "tables": domain_info.get("tables", []),
                    "primary_table": domain_info.get("primary_table"),
                    "source": "manual_mapping",
                    "reason": domain_info.get("reason", "Keyword matched in domain mapping")
                }
        
        return None
    
    def introspect_tables_from_keywords(self, question: str, allowed_tables: List[str]) -> Optional[Dict]:
        """
        Use table name similarity matching when no manual mapping exists.
        This would ideally query INFORMATION_SCHEMA.TABLES and COLUMNS.
        
        For MVP, we do fuzzy matching on table names.
        Future: Query actual database schema.
        """
        if not self.domain_mappings.get("schema_introspection", {}).get("enabled", False):
            return None
        
        words = question.lower().split()
        best_match = None
        best_score = 0.0
        threshold = self.domain_mappings.get("schema_introspection", {}).get("confidence_threshold", 0.6)
        
        # Try to match question words with table names
        for table in allowed_tables:
            table_lower = table.lower()
            for word in words:
                # Skip common words
                if word in ["the", "and", "or", "show", "list", "get", "me", "all", "my"]:
                    continue
                
                score = self._similarity(word, table_lower)
                if score > best_score:
                    best_score = score
                    best_match = table
        
        if best_match and best_score >= threshold:
            # Infer related tables (this is simplified - real implementation would query foreign keys)
            related_tables = self._infer_related_tables(best_match, allowed_tables)
            
            return {
                "domain": best_match.lower(),
                "tables": [best_match] + related_tables,
                "primary_table": best_match,
                "source": "schema_introspection",
                "confidence": best_score,
                "reason": f"Fuzzy matched '{best_match}' with confidence {best_score:.2f}"
            }
        
        return None
    
    def _similarity(self, a: str, b: str) -> float:
        """Calculate string similarity ratio"""
        return SequenceMatcher(None, a, b).ratio()
    
    def _infer_related_tables(self, primary_table: str, allowed_tables: List[str]) -> List[str]:
        """
        Return related tables using real FK relationships from INFORMATION_SCHEMA.
        Falls back to heuristic rules when DB is unavailable.
        """
        # Prefer real FK-based lookup
        if self._info_schema is not None:
            fk_related = self._info_schema.get_related_tables(primary_table, allowed_tables)
            if fk_related:
                logger.debug(f"[SchemaIntrospector] FK-based related tables for {primary_table}: {fk_related}")
                return fk_related[:3]

        # Heuristic fallback
        related: List[str] = []
        if "Properties" in allowed_tables and primary_table != "Properties":
            related.append("Properties")
        if primary_table == "Inspections" and "Tenancies" in allowed_tables:
            related.append("Tenancies")

        logger.debug(f"[SchemaIntrospector] Heuristic related tables for {primary_table}: {related}")
        return related[:3]
    
    def get_table_info_for_question(self, question: str, allowed_tables: List[str]) -> Dict:
        """
        Main method: Try manual mapping first, fallback to schema introspection.
        """
        # Try manual mapping first
        result = self.find_domain_by_keywords(question)
        if result:
            # Filter to only allowed tables
            result["tables"] = [t for t in result["tables"] if t in allowed_tables]
            return result
        
        # Fallback to schema introspection
        result = self.introspect_tables_from_keywords(question, allowed_tables)
        if result:
            return result
        
        # Ultimate fallback: general domain with first few allowed tables
        return {
            "domain": "general",
            "tables": allowed_tables[:6],
            "primary_table": allowed_tables[0] if allowed_tables else "Properties",
            "source": "fallback",
            "reason": "No mapping or introspection match found"
        }


# Module-level cache
_introspector = None

def get_introspector() -> SchemaIntrospector:
    """Get singleton instance of SchemaIntrospector"""
    global _introspector
    if _introspector is None:
        _introspector = SchemaIntrospector()
    return _introspector
