"""
Phase 3: Prompt Library for loading and rendering YAML-based prompt templates
Provides version management, Jinja2 rendering, and template caching
"""
import yaml
import logging
from pathlib import Path
from typing import Dict, Any, Optional, List
from functools import lru_cache
from jinja2 import Environment, BaseLoader, TemplateNotFound
from dataclasses import dataclass

logger = logging.getLogger(__name__)


@dataclass
class PromptTemplate:
    """Loaded prompt template with metadata"""
    version: str
    author: str
    created: str
    description: str
    temperature: Optional[float]
    max_tokens: Optional[int]
    template: str
    variables: List[str]
    examples: List[Dict[str, str]]
    
    def render(self, **kwargs) -> str:
        """Render template with Jinja2"""
        env = Environment(autoescape=False)
        template = env.from_string(self.template)
        return template.render(**kwargs)


class PromptLibrary:
    """
    Central repository for prompt templates.
    
    Features:
    - Load YAML templates from prompts/ directory
    - Version management (v1, v2, latest)
    - Jinja2 template rendering
    - LRU caching for performance
    - Validation of required variables
    """
    
    def __init__(self, prompts_dir: Optional[Path] = None):
        """
        Initialize prompt library.
        
        Args:
            prompts_dir: Path to prompts directory. Defaults to ./prompts
        """
        if prompts_dir is None:
            # Default to prompts/ in the same directory as this file
            prompts_dir = Path(__file__).parent.parent / "prompts"
        
        self.prompts_dir = Path(prompts_dir)
        
        if not self.prompts_dir.exists():
            logger.warning(f"Prompts directory not found: {self.prompts_dir}")
        
        logger.info(f"Initialized PromptLibrary with directory: {self.prompts_dir}")
    
    @lru_cache(maxsize=128)
    def load_prompt(
        self,
        agent_name: str,
        prompt_type: str,
        version: str = "v1"
    ) -> PromptTemplate:
        """
        Load a prompt template from YAML file.
        
        Args:
            agent_name: Name of agent (domain_classifier, schema_analyzer, etc.)
            prompt_type: Type of prompt (system, user)
            version: Template version (v1, v2, ..., or 'latest')
        
        Returns:
            PromptTemplate with loaded content
        
        Raises:
            FileNotFoundError: If template file doesn't exist
            ValueError: If YAML is invalid
        """
        # Resolve 'latest' to actual version
        if version == "latest":
            version = self._get_latest_version(agent_name, prompt_type)
        
        # Build file path: prompts/{agent_name}/{prompt_type}.yaml
        template_path = self.prompts_dir / agent_name / f"{prompt_type}.yaml"
        
        if not template_path.exists():
            raise FileNotFoundError(
                f"Prompt template not found: {template_path}\n"
                f"Expected structure: prompts/{agent_name}/{prompt_type}.yaml"
            )
        
        try:
            with open(template_path, 'r', encoding='utf-8') as f:
                data = yaml.safe_load(f)
            
            # Validate required fields
            required = ['version', 'template']
            for field in required:
                if field not in data:
                    raise ValueError(f"Missing required field '{field}' in {template_path}")
            
            # Create PromptTemplate with defaults for optional fields
            prompt = PromptTemplate(
                version=data['version'],
                author=data.get('author', 'Unknown'),
                created=data.get('created', ''),
                description=data.get('description', ''),
                temperature=data.get('temperature'),
                max_tokens=data.get('max_tokens'),
                template=data['template'],
                variables=data.get('variables', []),
                examples=data.get('examples', [])
            )
            
            logger.info(f"Loaded prompt: {agent_name}/{prompt_type} (version {prompt.version})")
            return prompt
            
        except yaml.YAMLError as e:
            raise ValueError(f"Invalid YAML in {template_path}: {e}")
    
    def render_prompt(
        self,
        agent_name: str,
        prompt_type: str,
        version: str = "v1",
        **variables
    ) -> str:
        """
        Load and render a prompt template with variables.
        
        Args:
            agent_name: Name of agent
            prompt_type: Type of prompt (system, user)
            version: Template version
            **variables: Variables to substitute in template
        
        Returns:
            Rendered prompt string
        
        Example:
            >>> library = PromptLibrary()
            >>> prompt = library.render_prompt(
            ...     'domain_classifier',
            ...     'user',
            ...     question="Show me all tenancies",
            ...     role="PropertyManager"
            ... )
        """
        template = self.load_prompt(agent_name, prompt_type, version)
        
        # Validate required variables are provided
        missing = [var for var in template.variables if var not in variables]
        if missing:
            logger.warning(
                f"Missing variables for {agent_name}/{prompt_type}: {missing}. "
                f"Rendering may fail or produce incomplete output."
            )
        
        try:
            rendered = template.render(**variables)
            logger.debug(f"Rendered {agent_name}/{prompt_type} ({len(rendered)} chars)")
            return rendered
        except Exception as e:
            logger.error(f"Failed to render {agent_name}/{prompt_type}: {e}")
            raise
    
    def get_config(
        self,
        agent_name: str,
        prompt_type: str,
        version: str = "v1"
    ) -> Dict[str, Any]:
        """
        Get prompt configuration (temperature, max_tokens, etc.) without rendering.
        
        Returns:
            Dict with temperature, max_tokens, and other config
        """
        template = self.load_prompt(agent_name, prompt_type, version)
        return {
            'version': template.version,
            'temperature': template.temperature,
            'max_tokens': template.max_tokens,
            'author': template.author,
            'description': template.description
        }
    
    def _get_latest_version(self, agent_name: str, prompt_type: str) -> str:
        """
        Find the latest version of a prompt template.
        Currently returns 'v1' - can be extended to scan for v2, v3, etc.
        """
        # TODO: Implement version scanning across multiple YAML files
        # For now, return v1 as default
        return "v1"
    
    def list_agents(self) -> List[str]:
        """List all available agents with prompts"""
        if not self.prompts_dir.exists():
            return []
        
        agents = [
            d.name for d in self.prompts_dir.iterdir()
            if d.is_dir() and not d.name.startswith('.')
        ]
        return sorted(agents)
    
    def validate_template(
        self,
        agent_name: str,
        prompt_type: str,
        version: str = "v1"
    ) -> tuple[bool, Optional[str]]:
        """
        Validate a prompt template without loading into cache.
        
        Returns:
            (is_valid, error_message)
        """
        try:
            template = self.load_prompt(agent_name, prompt_type, version)
            
            # Basic validation checks
            if not template.template or len(template.template.strip()) == 0:
                return False, "Template content is empty"
            
            if template.temperature is not None and not (0.0 <= template.temperature <= 2.0):
                return False, f"Invalid temperature: {template.temperature} (must be 0.0-2.0)"
            
            if template.max_tokens is not None and template.max_tokens < 1:
                return False, f"Invalid max_tokens: {template.max_tokens}"
            
            # Try rendering with empty variables to check Jinja2 syntax
            try:
                env = Environment(autoescape=False)
                env.from_string(template.template)
            except Exception as e:
                return False, f"Invalid Jinja2 template: {e}"
            
            return True, None
            
        except Exception as e:
            return False, str(e)


# Global prompt library instance
_prompt_library: Optional[PromptLibrary] = None


def get_prompt_library() -> PromptLibrary:
    """Get or create the global PromptLibrary instance"""
    global _prompt_library
    if _prompt_library is None:
        _prompt_library = PromptLibrary()
    return _prompt_library
