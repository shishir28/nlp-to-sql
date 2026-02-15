"""
Schema introspection service for discovering database tables and columns
using MySQL INFORMATION_SCHEMA when manual mappings don't exist.
"""
import json
from pathlib import Path
from typing import Dict, List, Optional
from difflib import SequenceMatcher


class SchemaIntrospector:
    """Discovers relevant tables and columns from database schema"""
    
    def __init__(self, mapping_file: str = "domain-mapping.json"):
        self.mapping_file = Path(__file__).parent.parent / mapping_file
        self.domain_mappings = self._load_mappings()
        
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
        Infer related tables based on common patterns.
        In production, this would query INFORMATION_SCHEMA.KEY_COLUMN_USAGE for foreign keys.
        """
        related = []
        
        # Always include Properties if available (common join)
        if "Properties" in allowed_tables and primary_table != "Properties":
            related.append("Properties")
        
        # Domain-specific heuristics
        if primary_table == "Inspections":
            if "Tenancies" in allowed_tables and "Tenancies" not in related:
                related.append("Tenancies")
        
        # Limit to 3 additional tables
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
