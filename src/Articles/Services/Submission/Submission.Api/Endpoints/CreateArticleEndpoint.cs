using Articles.Abstractions.Enums;
using Articles.Security;
using MediatR;
using Submission.Application.Features.CreateArticle;

namespace Submission.Api.Endpoints;

public static class CreateArticleEndpoint
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/articles", async (
                CreateArticleCommand command,
                ISender sender
            ) =>
            {
                var response = await sender.Send(command);
                return Results.Created($"/api/articles/{response.Id}", response);
            })
            .RequireAuthorization(policy => policy.RequireRole(Role.AUT))
            .WithName("CreateArticle")
            .WithTags("Articles")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem((StatusCodes.Status401Unauthorized))
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }
}