using System.ComponentModel;

namespace Articles.Abstractions.Enums;

public enum UserRoleType
{
    // Cross-domain: 1–9
    [Description("Editorial Office Admin")]
    EOF = 1,

    // Submission: 11–19
    [Description("Author")] AUT = 11,
    [Description("Corresponding Author")] CORAUT = 12,

    // Review: 21–29
    [Description("Review Editor")] REVED = 21,
    [Description("Reviewer")] REV = 22,

    // Production: 31–39
    [Description("Production Office Admin")]
    POF = 31,
    [Description("Typesetter")] TSOF = 32,

    //insight - explain the ranges 11-19 and also the gap betwen the last domain(production) to Auth, allow space for other domains/microservices 

    // Auth-only: 91–99
    [Description("User Admin")] USERADMIN = 91
}

public static class Role
{
    public const string EOF = nameof(UserRoleType.EOF);
    public const string CORAUT = nameof(UserRoleType.CORAUT);
    public const string AUT = nameof(UserRoleType.AUT);
    public const string USERADMIN = nameof(UserRoleType.USERADMIN);
}