using Auth.Grpc;
using ProtoBuf.Grpc;

namespace Auth.API.Features.Persons;

public class PersonGrpcService(GetPersonByUserIdRequest request, CallContext context = default)
    : IPersonService
{
}