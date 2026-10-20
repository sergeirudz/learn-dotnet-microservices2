using Articles.Abstractions.Events;
using Articles.Abstractions.Events.Dtos;
using Blocks.Domain;
using Blocks.Exceptions;
using FileStorage.Contracts;
using MassTransit;
using Review.Application.FileStorage;
using Review.Domain.Assets;
using Article = Review.Domain.Articles.Behaviors.Article;

namespace Review.Application.Features.Articles.InitializeFromSubmission;

public class ArticleApprovedForReviewEventHandler(
    ReviewDbContext _dbContext,
    ArticleRepository _articleRepository,
    Repository<Person> _personRepository,
    Repository<Journal> _journalRepository,
    AssetTypeDefinitionRepository _assetTypeDefinition,
    IFileService _fileService,
    FileServiceFactory _fileServiceFactory
    // ArticleStateMachineFactory _stateMachineFactory
)
    : IConsumer<ArticleApprovedForReviewEvent>
{
    public async Task Consume(ConsumeContext<ArticleApprovedForReviewEvent> context)
    {
        var articleDto = context.Message.Article;
        if (_articleRepository.Entity.Any(e => e.Id == articleDto.Id))
        {
            throw new BadRequestException($"Article({articleDto.Id}) already exists");
        }

        var journal = await GetOrCreateJournal(articleDto);
        var actors = await CreateActors(articleDto);
        var assets = await CreateAssets(articleDto);

        var article = Article.ArticleSubmitted(articleDto, actors, assets);

        journal.AddArticle(article);

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }

    private async Task<IEnumerable<Asset>> CreateAssets(ArticleDto articleDto)
    {
        var assets = new List<Asset>();
        foreach (var assetDto in articleDto.Assets)
        {
            var assetTypeDefinition = _assetTypeDefinition.GetById(assetDto.Type);
            var asset = Asset.CreateFromSubmission(assetDto, assetTypeDefinition, articleDto.Id);

            // todo - download the foles from submission and upload to review
            var submissionFileService = _fileServiceFactory(FileStorageType.Submission);
            var (fileStream, fileMetadata) = await submissionFileService.DownloadAsync(assetDto.File.FileServerId);

            var uploadRequest = new FileUploadRequest(
                fileMetadata.StoragePath, fileMetadata.FileName,
                fileMetadata.ContentType, fileMetadata.FileSize);
            fileMetadata = await _fileService.UploadAsync(uploadRequest, fileStream);

            asset.CreateFile(fileMetadata, assetTypeDefinition);

            assets.Add(asset);
        }

        return assets;
    }

    private async Task<IEnumerable<ArticleActor>> CreateActors(object articleDto)
    {
        var actors = new List<ArticleActor>();
        foreach (var actorDto in articleDto.Actors)
        {
            var person = await _personRepository.GetByIdAsync(actorDto.Person.Id);

            ArticleActor actor = default!;
            if (actorDto.Role == UserRoleType.AUT || actorDto.Role == UserRoleType.CORAUT)
            {
                if (person is null)
                {
                    person = actorDto.Person.Adapt<Author>();
                    actor = new ArticleAuthor(actorDto.ContributionAreas)
                    {
                        PersonId = person.Id,
                        Person = person,
                        Role = actorDto.Role
                    };
                }
            }


            else if (actorDto.Role == UserRoleType.REVED)
            {
                if (person is null)
                {
                    person = actorDto.Person.Adapt<Author>();
                    actor = new ArticleActor
                    {
                        PersonId = person.Id,
                        Person = person,
                        Role = actorDto.Role
                    };
                }
            }
            else
            {
                // decide : throw or just log a warning
                throw new DomainException($"Unknow role for {actorDto.Person.Email}");
            }

            actors.Add(actor);
        }

        return actors;
    }

    private async Task<Journal> GetOrCreateJournal(ArticleDto articleDto)
    {
        var journal = await _journalRepository.FindByIdAsync(articleDto.Journal.Id);
        if (journal is null)
        {
            journal = articleDto.Journal.Adapt<Journal>();
            await _journalRepository.AddAsync(journal);
        }

        return journal;
    }
}