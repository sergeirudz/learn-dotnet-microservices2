using Articles.Abstractions;
using Articles.Abstractions.Enums;
using Blocks.Exceptions;
using Blocks.Redis;
using Microsoft.AspNetCore.Authorization;
using FastEndpoints;
using Journals.Domain.Journals;
using Journals.Domain.Journals.Events;
using Mapster;

namespace Journals.API.Features.Journals.Create;

[Authorize(Roles = Role.EOF)]
[HttpPost("journals")]
[Tags("Journals")]
public class CreateJournalEndpoint(
    Repository<Journal> _journalRepository,
    Repository<Editor> _editorRepository
)
    : Endpoint<CreateJournalCommand, IdResponse>
{
    public override async Task HandleAsync(CreateJournalCommand command, CancellationToken ct)
    {
        if (_journalRepository.Collection.Any(j =>
                j.Abbreviation == command.Abbreviation || j.Name == command.Name))
            throw new BadRequestException("Journal with the same name or abbreviation already exists");

        if (!_editorRepository.Collection.Any(e => e.Id == command.ChiefEditorId))
        {
            // await CreateEditor(command.ChiefEditorId, ct);
        }

        var journal = command.Adapt<Journal>();

        await _journalRepository.AddAsync(journal);

        await PublishAsync(new JournalCreated(journal));

        await Send.OkAsync(new IdResponse(journal.Id));
    }
}