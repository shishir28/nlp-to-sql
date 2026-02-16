"""
Phase 3 Test Suite: Validate Pydantic models and PromptLibrary
Run this to verify Phase 3 improvements before deployment
"""
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from app.state_models import LLMAgentState, Domain, QueryComplexity
from app.prompt_library import PromptLibrary, get_prompt_library
from app.llm_graph import MetricsCollector
import json


def test_pydantic_models():
    """Test Pydantic state models with validation"""
    print("=" * 60)
    print("TEST 1: Pydantic State Models")
    print("=" * 60)
    
    # Test 1: Valid state creation
    print("\n✓ Creating valid state...")
    try:
        state = LLMAgentState(
            question="Show me all tenancies with arrears",
            customer_id="C123",
            user_id="U456",
            role="PropertyManager",
            tenant_column="CustomerId",
            default_limit=50,
            allowed_tables=["Tenancies", "Payments"],
            detected_domain="arrears",
            domain_confidence=0.95,
            confidence=0.90
        )
        print(f"  ✓ State created: {state.question[:50]}...")
        print(f"  ✓ Domain: {state.detected_domain}, Confidence: {state.confidence}")
        assert state.confidence == 0.90
        assert state.domain_confidence == 0.95
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 2: Validation - confidence bounds
    print("\n✓ Testing confidence validation...")
    try:
        state_high = LLMAgentState(
            question="test",
            customer_id="C123",
            user_id="U456",
            role="Tenant",
            confidence=1.5  # Should be clamped to 1.0
        )
        assert state_high.confidence == 1.0, f"Expected 1.0, got {state_high.confidence}"
        print(f"  ✓ High confidence (1.5) clamped to: {state_high.confidence}")
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 3: Validation - limit bounds
    print("\n✓ Testing limit validation...")
    try:
        state_limit = LLMAgentState(
            question="test",
            customer_id="C123",
            user_id="U456",
            role="Tenant",
            extracted_limit=5000  # Should be clamped to 1000
        )
        assert state_limit.extracted_limit == 1000
        print(f"  ✓ High limit (5000) clamped to: {state_limit.extracted_limit}")
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 4: Convert to dict (for LangGraph)
    print("\n✓ Testing model_dump() for LangGraph...")
    try:
        state_dict = state.model_dump()
        assert isinstance(state_dict, dict)
        assert state_dict['question'] == state.question
        assert 'metrics_collector' in state_dict
        print(f"  ✓ Converted to dict with {len(state_dict)} keys")
        print(f"  ✓ Sample keys: {list(state_dict.keys())[:5]}")
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 5: Enum values
    print("\n✓ Testing enum types...")
    try:
        state.query_complexity = QueryComplexity.MEDIUM
        assert state.query_complexity == "medium"  # use_enum_values=True
        print(f"  ✓ QueryComplexity enum: {state.query_complexity}")
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    print("\n✓ All Pydantic model tests passed!\n")
    return True


def test_prompt_library():
    """Test PromptLibrary loading and rendering"""
    print("=" * 60)
    print("TEST 2: PromptLibrary")
    print("=" * 60)
    
    library = get_prompt_library()
    
    # Test 1: List available agents
    print("\n✓ Listing available agents...")
    try:
        agents = library.list_agents()
        print(f"  ✓ Found {len(agents)} agents: {agents}")
        assert 'domain_classifier' in agents
        assert 'sql_generator' in agents
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 2: Load system prompt
    print("\n✓ Loading domain_classifier system prompt...")
    try:
        template = library.load_prompt('domain_classifier', 'system', version='v1')
        print(f"  ✓ Version: {template.version}")
        print(f"  ✓ Author: {template.author}")
        print(f"  ✓ Temperature: {template.temperature}")
        print(f"  ✓ Template length: {len(template.template)} chars")
        print(f"  ✓ Variables: {template.variables}")
        assert template.temperature == 0.1
        assert len(template.template) > 0
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 3: Load user prompt
    print("\n✓ Loading domain_classifier user prompt...")
    try:
        template = library.load_prompt('domain_classifier', 'user', version='v1')
        print(f"  ✓ Variables required: {template.variables}")
        assert 'question' in template.variables
        assert 'role' in template.variables
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 4: Render prompt with variables
    print("\n✓ Rendering prompt with Jinja2...")
    try:
        rendered = library.render_prompt(
            'domain_classifier',
            'user',
            version='v1',
            question="Which tenancies have arrears?",
            allowed_tables=["Tenancies", "Payments"],
            role="PropertyManager"
        )
        print(f"  ✓ Rendered length: {len(rendered)} chars")
        assert "Which tenancies have arrears?" in rendered
        assert "PropertyManager" in rendered
        print(f"  ✓ Preview: {rendered[:100]}...")
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 5: Get configuration
    print("\n✓ Getting prompt configuration...")
    try:
        config = library.get_config('sql_generator', 'system')
        print(f"  ✓ Config: {json.dumps(config, indent=2)}")
        assert config['temperature'] == 0.0  # SQL generator should be deterministic
        assert config['max_tokens'] == 1500
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False
    
    # Test 6: Validate all templates
    print("\n✓ Validating all templates...")
    all_valid = True
    for agent in ['domain_classifier', 'schema_analyzer', 'sql_generator', 'sql_validator']:
        for prompt_type in ['system', 'user']:
            try:
                is_valid, error = library.validate_template(agent, prompt_type)
                if is_valid:
                    print(f"  ✓ {agent}/{prompt_type}: valid")
                else:
                    print(f"  ✗ {agent}/{prompt_type}: {error}")
                    all_valid = False
            except Exception as e:
                print(f"  ✗ {agent}/{prompt_type}: {e}")
                all_valid = False
    
    if not all_valid:
        return False
    
    print("\n✓ All PromptLibrary tests passed!\n")
    return True


def test_integration():
    """Test integration between Pydantic models and PromptLibrary"""
    print("=" * 60)
    print("TEST 3: Integration Test")
    print("=" * 60)
    
    # Create a realistic state
    print("\n✓ Creating realistic agent state...")
    try:
        metrics = MetricsCollector()
        
        state = LLMAgentState(
            question="Which tenancies have arrears over $500?",
            customer_id="CUST123",
            user_id="USER456",
            role="PropertyManager",
            tenant_column="CustomerId",
            default_limit=50,
            allowed_tables=["Tenancies", "Payments", "Properties"],
            detected_domain="arrears",
            domain_confidence=0.95,
            scoped_tables=["Tenancies", "Payments"],
            metrics_collector=metrics,
            extracted_limit=100,
            extracted_filters={"RentAmount": {"operator": ">", "value": 500}}
        )
        print(f"  ✓ State created with {len(state.allowed_tables)} tables")
    except Exception as e:
        print(f"  ✗ Failed to create state: {e}")
        return False
    
    # Use state data to render prompts
    print("\n✓ Rendering prompts with state data...")
    try:
        library = get_prompt_library()
        
        # Render domain classifier prompt
        domain_prompt = library.render_prompt(
            'domain_classifier',
            'user',
            question=state.question,
            allowed_tables=state.allowed_tables,
            role=state.role
        )
        assert state.question in domain_prompt
        print(f"  ✓ Domain classifier prompt: {len(domain_prompt)} chars")
        
        # Render schema analyzer prompt
        schema_prompt = library.render_prompt(
            'schema_analyzer',
            'user',
            question=state.question,
            domain=state.detected_domain,
            domain_confidence=state.domain_confidence,
            scoped_tables=state.scoped_tables,
            schema_details="-- Sample schema --",
            tenant_column=state.tenant_column,
            customer_id=state.customer_id,
            extracted_filters=state.extracted_filters
        )
        assert "arrears" in schema_prompt
        assert "RentAmount" in schema_prompt
        print(f"  ✓ Schema analyzer prompt: {len(schema_prompt)} chars")
        
    except Exception as e:
        print(f"  ✗ Failed to render prompts: {e}")
        return False
    
    print("\n✓ Integration test passed!\n")
    return True


def main():
    """Run all Phase 3 tests"""
    print("\n" + "=" * 60)
    print("PHASE 3 TEST SUITE")
    print("Testing: Pydantic Models + PromptLibrary")
    print("=" * 60 + "\n")
    
    results = []
    
    # Run tests
    results.append(("Pydantic Models", test_pydantic_models()))
    results.append(("PromptLibrary", test_prompt_library()))
    results.append(("Integration", test_integration()))
    
    # Summary
    print("=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    for name, passed in results:
        status = "✓ PASSED" if passed else "✗ FAILED"
        print(f"{name:.<50} {status}")
    
    all_passed = all(r[1] for r in results)
    
    if all_passed:
        print("\n✓ All Phase 3 tests passed! System is ready for deployment.")
        return 0
    else:
        print("\n✗ Some tests failed. Please review and fix issues.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
