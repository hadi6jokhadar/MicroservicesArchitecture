"""add_audio_duration_seconds_to_token_usage_log

Revision ID: b7c8d9e0f1a2
Revises: a1b2c3d4e5f6
Create Date: 2026-08-03 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'b7c8d9e0f1a2'
down_revision: Union[str, None] = 'a1b2c3d4e5f6'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        'AiTokenUsageLog',
        sa.Column('AudioDurationSeconds', sa.Float(), nullable=True)
    )


def downgrade() -> None:
    op.drop_column('AiTokenUsageLog', 'AudioDurationSeconds')
