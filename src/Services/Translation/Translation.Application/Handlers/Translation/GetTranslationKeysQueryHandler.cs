using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.DTOs;
using Translation.Application.Queries;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class GetTranslationKeysQueryHandler : IRequestHandler<GetTranslationKeysQuery, PaginatedList<TranslationKeyDto>>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ILogger<GetTranslationKeysQueryHandler> _logger;

    public GetTranslationKeysQueryHandler(
        ITranslationKeyRepository keyRepository,
        ILogger<GetTranslationKeysQueryHandler> logger)
    {
        _keyRepository = keyRepository;
        _logger = logger;
    }

    public async Task<PaginatedList<TranslationKeyDto>> Handle(GetTranslationKeysQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var paginatedKeys = await _keyRepository.GetPaginatedAsync(
                request.PageNumber,
                request.PageSize,
                request.Category,
                request.SearchTerm,
                request.IsArchived,
                request.TenantId,
                cancellationToken);

            var dtos = paginatedKeys.Items.Select(TranslationKeyDto.MapFrom).ToList();

            return new PaginatedList<TranslationKeyDto>(
                dtos,
                paginatedKeys.TotalCount,
                paginatedKeys.PageNumber,
                paginatedKeys.TotalPages);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting translation keys");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
