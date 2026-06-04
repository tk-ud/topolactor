namespace Topolactor.Schema;

/// <summary>Canonical realm / audience / role for JWT and session binding.</summary>
public static class AuthRealm
{
    public const string UserRealm = "user";
    public const string UserAudience = "user_app";
    public const string UserRole = "user";

    public const string AdminRealm = "admin/system";
    public const string AdminAudience = "admin_console";
    public const string AdminRole = "admin";

    public static AuthRealmContext User => new(UserRealm, UserAudience, UserRole);
    public static AuthRealmContext Admin => new(AdminRealm, AdminAudience, AdminRole);
}

public record AuthRealmContext(string Realm, string Audience, string Role);
