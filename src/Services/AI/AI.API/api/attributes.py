"""
Python equivalents of .NET tenant attributes used by route handlers.

- @bypass_tenant: tenant resolution is skipped completely.
- @optional_tenant: tenant resolution runs if available, but does not fail when missing.
"""

from collections.abc import Callable
from typing import TypeVar


F = TypeVar("F", bound=Callable)


def bypass_tenant(func: F) -> F:
    """Marks a route handler as bypassing tenant resolution."""
    setattr(func, "_bypass_tenant", True)
    return func


def optional_tenant(func: F) -> F:
    """Marks a route handler where tenant context is optional."""
    setattr(func, "_optional_tenant", True)
    return func
