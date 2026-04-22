"""add_key_column_to_ai_provider_settings

Revision ID: b1c4e8d9f2a3
Revises: 0ac3b2ff2846
Create Date: 2026-04-22 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'b1c4e8d9f2a3'
down_revision: Union[str, None] = '0ac3b2ff2846'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # Add Key column as nullable first to allow populating existing rows
    op.add_column('AiProviderSettings', sa.Column('Key', sa.String(length=100), nullable=True))
    # Populate existing rows with a default key derived from their Id
    op.execute("UPDATE \"AiProviderSettings\" SET \"Key\" = CAST(\"Id\" AS TEXT) WHERE \"Key\" IS NULL")
    # Now enforce NOT NULL and unique constraints
    op.alter_column('AiProviderSettings', 'Key', nullable=False)
    op.create_unique_constraint('uq_ai_provider_settings_key', 'AiProviderSettings', ['Key'])
    op.create_index('ix_AiProviderSettings_Key', 'AiProviderSettings', ['Key'], unique=True)


def downgrade() -> None:
    op.drop_index('ix_AiProviderSettings_Key', table_name='AiProviderSettings')
    op.drop_constraint('uq_ai_provider_settings_key', 'AiProviderSettings', type_='unique')
    op.drop_column('AiProviderSettings', 'Key')
