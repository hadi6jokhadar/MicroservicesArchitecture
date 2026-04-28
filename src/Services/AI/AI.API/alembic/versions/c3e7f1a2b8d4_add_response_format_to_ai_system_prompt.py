"""add_response_format_to_ai_system_prompt

Revision ID: c3e7f1a2b8d4
Revises: b1c4e8d9f2a3
Create Date: 2026-04-27 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'c3e7f1a2b8d4'
down_revision: Union[str, None] = 'd27084ec4fea'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column('AiSystemPrompt', sa.Column('ResponseFormat', sa.Text(), nullable=True))


def downgrade() -> None:
    op.drop_column('AiSystemPrompt', 'ResponseFormat')
