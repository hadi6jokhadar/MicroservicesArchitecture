using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Translation.Application.DTOs;
using Translation.Application.Queries;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class GetTranslationKeysQueryHandler : IRequestHandler<GetTranslationKeysQuery, PaginatedList<TranslationKeyDto>>
{
    private readonly ITranslationKeyRepository _keyRepository;

    public GetTranslationKeysQueryHandler(ITranslationKeyRepository keyRepository)
    {
        _keyRepository = keyRepository;
    }

    public async Task<PaginatedList<TranslationKeyDto>> Handle(GetTranslationKeysQuery request, CancellationToken cancellationToken)
    {
        var paginatedKeys = await _keyRepository.GetPaginatedAsync(
            request.PageNumber,
            request.PageSize,
            request.Category,
            request.SearchTerm,
            request.IsArchived,
            cancellationToken);

        var dtos = paginatedKeys.Items.Select(TranslationKeyDto.MapFrom).ToList();

        return new PaginatedList<TranslationKeyDto>(
            dtos,
            paginatedKeys.TotalCount,
            paginatedKeys.PageNumber,
            paginatedKeys.TotalPages);
    }
}
