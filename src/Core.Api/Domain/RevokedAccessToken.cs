namespace Core.Api.Domain;

public class RevokedAccessToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Jti { get; set; } = default!;
    public DateTime RevokedAt { get; set; }
}