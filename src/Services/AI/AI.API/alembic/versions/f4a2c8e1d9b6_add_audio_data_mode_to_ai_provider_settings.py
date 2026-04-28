"""add_audio_data_mode_to_ai_provider_settings

Revision ID: f4a2c8e1d9b6
Revises: bd0e13c312b3
Create Date: 2026-04-28 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'f4a2c8e1d9b6'
down_revision: Union[str, None] = 'bd0e13c312b3'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # Create the enum type first, then add the column.
    audio_data_mode_enum = sa.Enum('Auto', 'Url', 'Base64', name='audiodatamodeEnum')
    audio_data_mode_enum.create(op.get_bind(), checkfirst=True)

    op.add_column(
        'AiProviderSettings',
        sa.Column(
            'AudioDataMode',
            sa.Enum('Auto', 'Url', 'Base64', name='audiodatamodeEnum', create_type=False),
            nullable=True,
        ),
    )


def downgrade() -> None:
    op.drop_column('AiProviderSettings', 'AudioDataMode')
    sa.Enum(name='audiodatamodeEnum').drop(op.get_bind(), checkfirst=True)
