"""add_no_speech_prob_threshold_to_ai_provider_settings

Revision ID: a4d8f2c6e1b9
Revises: b7c8d9e0f1a2
Create Date: 2026-08-06 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'a4d8f2c6e1b9'
down_revision: Union[str, None] = 'b7c8d9e0f1a2'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column('AiProviderSettings', sa.Column('NoSpeechProbThreshold', sa.Float(), nullable=True))


def downgrade() -> None:
    op.drop_column('AiProviderSettings', 'NoSpeechProbThreshold')
