using Auth.Domain.Persons;
using Auth.Grpc;
using Auth.Persistence.Repositories;
using ProtoBuf.Grpc;

namespace Auth.API.Features.Persons;

public class PersonGrpcService(PersonRepositoryBase personRepositoryBase, GrpcTypeAdapterConfig _typeAdapterConfig)
    : IPersonService
{
    public async ValueTask<CreatePersonResponse> GetOrCreatePersonAsync(CreatePersonRequest request,
        CallContext context = default)
    {
        var person = await personRepositoryBase.GetByEmailAsync(request.Email, context.CancellationToken);

        if (person is null)
        {
            person = Person.Create(request);

            await personRepositoryBase.AddAsync(person);
            await personRepositoryBase.SaveChangesAsync(context.CancellationToken);
        }

        //todo - validate CreatePersonRequest, create an HTTP endpoint and use the same handler and validator
        return new CreatePersonResponse
        {
            // Mapster, adapt the Person domain aggregate into PersonInfo GRPC contract
            PersonInfo = person.Adapt<PersonInfo>(_typeAdapterConfig)
        };
    }

    public async ValueTask<GetPersonResponse> GetPersonByIdAsync(GetPersonRequest request,
        CallContext context = default)
        => await GetPersonResponseAsync(() => personRepositoryBase.GetByIdAsync(request.PersonId));

    public async ValueTask<GetPersonResponse> GetPersonByUserIdAsync(GetPersonByUserIdRequest request,
        CallContext context = default)
        => await GetPersonResponseAsync(() => personRepositoryBase.GetByUserIdAsync(request.UserId));

    public async ValueTask<GetPersonResponse> GetPersonByEmailAsync(GetPersonByEmailRequest request,
        CallContext context = default)
        => await GetPersonResponseAsync(() => personRepositoryBase.GetByEmailAsync(request.Email));

    private async ValueTask<GetPersonResponse> GetPersonResponseAsync(Func<Task<Person?>> fetch)
    {
        var person = Guard.NotFound(await fetch());

        return new GetPersonResponse
        {
            PersonInfo = person.Adapt<PersonInfo>(_typeAdapterConfig)
        };
    }
}