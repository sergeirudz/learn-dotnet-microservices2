using Auth.Grpc;
using Mapster;

namespace Auth.Domain.Persons;

// Then Mapster.Adapt() can 
// adapt the Person domain aggregate into PersonInfo GRPC contract
public class GrpcTypeAdapterConfig : TypeAdapterConfig
{
    public GrpcTypeAdapterConfig()
    {
        // todo: this is for standard GRPC, we can safely remove it
        this.NewConfig<Person, PersonInfo>()
            .Map(
                dest => dest.Honorific,
                src => src.Honorific == null ? null : src.Honorific.Value)
            .IgnoreNullValues(true);


        this.NewConfig<Auth.Domain.Persons.ValueObjects.ProfessionalProfile, ProfessionalProfile>();
    }
}