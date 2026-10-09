using Auth.Grpc;

namespace Submission.Domain.Entities;

public partial class Person
{
    public static Person Create(PersonInfo personInfo)
    {
        return new Person
        {
            UserId = personInfo.Id,
            Email = EmailAddress.Create(personInfo.Email),
            FirstName = personInfo.FirstName,
            LastName = personInfo.LastName,
            Affiliation = personInfo.ProfessionalProfile!.Affiliation,
        };
    }
}