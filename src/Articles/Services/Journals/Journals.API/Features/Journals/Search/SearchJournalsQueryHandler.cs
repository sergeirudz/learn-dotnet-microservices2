using Articles.Abstractions.Enums;
using Articles.Security;
using Blocks.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Redis.OM;
using Blocks.Redis;
using FastEndpoints;
using Journals.API.Features.Shared;
using Journals.Domain.Journals;
using Mapster;

namespace Journals.API.Features.Journals.Search;

[Authorize(Roles = $"{Role.EOF},{Role.REVED}")]
[HttpGet("journals")]
[Tags("Journals")]
public class SearchJournalsQueryHandler(Repository<Journal> _journalRepository, Repository<Editor> _editorRepository)
    : Endpoint<SearchJournalsQuery, SearchJournalsResponse>
{
    public override async Task HandleAsync(SearchJournalsQuery query, CancellationToken ct)
    {
        var collection = _journalRepository.Collection;
        if (!query.Search.IsNullOrEmpty())
        {
            var searchValue = query.Search.Trim().ToLowerInvariant();

            // Raw search is faster and more powerful than Linq search. Try both and compare.
            collection = RawSearch(searchValue, collection);
            //collection = Search(searchValue, collection);
        }

        var totalCount = collection.Count();

        var items = collection
            // .OrderBy(j => j.NormalizedName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new SearchJournalsResponse(
            query.Page,
            query.PageSize,
            totalCount,
            items.Select(j =>
            {
                var dto = j.Adapt<JournalDto>();
                dto.ChiefEditor = _editorRepository.GetById(j.ChiefEditorId).Adapt<EditorDto>();
                return dto;
            })
        );

        await Send.OkAsync(response, cancellation: ct);
    }

    private static Redis.OM.Searching.IRedisCollection<Journal> RawSearch(string searchText,
        Redis.OM.Searching.IRedisCollection<Journal> collection)
    {
        var queryString =
            $"(@Abbreviation:{{{searchText}}}) | " +
            $"((@Name:*{searchText}*) | (@Description:*{searchText}*))";

        // Redis allow to use this syntax to search
        collection = collection.Raw(queryString);
        return collection;
    }

    private static Redis.OM.Searching.IRedisCollection<Journal> Search(string searchText,
        Redis.OM.Searching.IRedisCollection<Journal> collection)
    {
        collection = collection.Where(j =>
            j.Abbreviation == searchText ||
            j.Name.Contains(searchText) ||
            j.Description.Contains(searchText));
        return collection;
    }
}