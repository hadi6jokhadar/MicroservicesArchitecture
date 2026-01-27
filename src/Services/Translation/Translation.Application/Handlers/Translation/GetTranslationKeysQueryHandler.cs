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
        var (items, totalCount) = await _keyRepository.GetPaginatedAsync(
            request.PageNumber,
            request.PageSize,
            request.Category,
            request.SearchTerm,
            cancellationToken);

        return new PaginatedList<TranslationKeyDto>
        {
            Items = items.Select(TranslationKeyDto.MapFrom).ToList(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
