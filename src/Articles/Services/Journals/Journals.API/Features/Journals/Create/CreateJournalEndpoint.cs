using Articles.Abstractions;
using Articles.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
using FastEndpoints;

namespace Journals.API.Features.Journals.Create;

[Authorize(Roles = Role.EOF)]
[HttpPost("journals")]
[Tags("Journals")]
public class CreateJournalEndpoint()
    : Endpoint<CreateJournalCommand, IdResponse>
{
    public override async Task HandleAsync(CreateJournalCommand command, CancellationToken ct)
    {
    }
}