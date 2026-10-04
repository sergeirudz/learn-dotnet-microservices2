using Articles.Abstractions;
using Auth.Domain.Persons.ValueObjects;
using Auth.Domain.Users;

namespace Auth.Domain.Persons;

public partial class Person
{
    public static Person Create(IPersonCreationInfo userInfo)
    {
        var person = new Person
        {
            FirstName = userInfo.FirstName,
            LastName = userInfo.LastName,
            Gender = userInfo.Gender,
            PictureUrl = userInfo.PictureUrl,
            Honorific = HonorificTitle.FromEnum(userInfo.Honorific),
            ProfessionalProfile =
                ProfessionalProfile.Create(userInfo.Position, userInfo.CompanyName, userInfo.Affiliation),
        };

        return person;
    }
}