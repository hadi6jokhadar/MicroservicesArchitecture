"""add_pipeline_run_id_to_chat_session_and_token_usage_log

Revision ID: b6e1a9c3d7f2
Revises: a4d8f2c6e1b9
Create Date: 2026-08-08 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'b6e1a9c3d7f2'
down_revision: Union[str, None] = 'a4d8f2c6e1b9'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column('AiChatSession', sa.Column('PipelineRunId', sa.String(200), nullable=True))
    op.create_index('ix_AiChatSession_PipelineRunId', 'AiChatSession', ['PipelineRunId'])

    op.add_column('AiTokenUsageLog', sa.Column('PipelineRunId', sa.String(200), nullable=True))
    op.create_index('ix_AiTokenUsageLog_PipelineRunId', 'AiTokenUsageLog', ['PipelineRunId'])


def downgrade() -> None:
    op.drop_index('ix_AiTokenUsageLog_PipelineRunId', table_name='AiTokenUsageLog')
    op.drop_column('AiTokenUsageLog', 'PipelineRunId')

    op.drop_index('ix_AiChatSession_PipelineRunId', table_name='AiChatSession')
    op.drop_column('AiChatSession', 'PipelineRunId')
