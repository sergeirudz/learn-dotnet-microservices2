using Articles.Abstractions.Events.Dtos;
using Mapster;
using Submission.Domain.ValueObjects;

namespace Submission.Application.Features.ApproveArticle;

public class IntegrationEventsMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ArticleActor, ActorDto>()
            .Include<ArticleAuthor, ActorDto>();

        config.NewConfig<Person, PersonDto>()
            .Include<Author, PersonDto>();

        // config.ForType<string, EmailAddress>()
        //     .MapWith(src => EmailAddress.Create(src));
    }
}