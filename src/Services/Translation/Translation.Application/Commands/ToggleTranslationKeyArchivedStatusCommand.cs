using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Commands;

/// <summary>
/// Command to toggle translation key archived status
/// </summary>
public record ToggleTranslationKeyArchivedStatusCommand(int Id) : IRequest<TranslationKeyDto>;
