"""add_createdat_to_settings_and_prompts

Revision ID: a1b2c3d4e5f6
Revises: f4a2c8e1d9b6
Create Date: 2026-05-10 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'a1b2c3d4e5f6'
down_revision: Union[str, None] = 'f4a2c8e1d9b6'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        'AiProviderSettings',
        sa.Column('CreatedAt', sa.DateTime(timezone=True), nullable=False, server_default=sa.text('now()'))
    )
    op.add_column(
        'AiSystemPrompt',
        sa.Column('CreatedAt', sa.DateTime(timezone=True), nullable=False, server_default=sa.text('now()'))
    )


def downgrade() -> None:
    op.drop_column('AiProviderSettings', 'CreatedAt')
    op.drop_column('AiSystemPrompt', 'CreatedAt')
