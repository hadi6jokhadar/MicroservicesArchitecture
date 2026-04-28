"""add_model_params_to_ai_provider_settings

Revision ID: e9f3a1c2d5b7
Revises: c3e7f1a2b8d4
Create Date: 2026-04-27 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'e9f3a1c2d5b7'
down_revision: Union[str, None] = 'c3e7f1a2b8d4'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column('AiProviderSettings', sa.Column('ApiBaseUrl', sa.String(500), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('Temperature', sa.Float(), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('Stream', sa.Boolean(), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('MaxCompletionTokens', sa.Integer(), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('TopP', sa.Float(), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('FrequencyPenalty', sa.Float(), nullable=True))
    op.add_column('AiProviderSettings', sa.Column('PresencePenalty', sa.Float(), nullable=True))


def downgrade() -> None:
    op.drop_column('AiProviderSettings', 'PresencePenalty')
    op.drop_column('AiProviderSettings', 'FrequencyPenalty')
    op.drop_column('AiProviderSettings', 'TopP')
    op.drop_column('AiProviderSettings', 'MaxCompletionTokens')
    op.drop_column('AiProviderSettings', 'Stream')
    op.drop_column('AiProviderSettings', 'Temperature')
    op.drop_column('AiProviderSettings', 'ApiBaseUrl')
