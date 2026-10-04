using Auth.API.Mappings;
using Auth.Grpc;
using Auth.Persistence.Repositories;
using ProtoBuf.Grpc;

namespace Auth.API.Features.Persons;

public class PersonGrpcService(GetPersonByUserIdRequest request, CallContext context = default)
    : IPersonService
{
}