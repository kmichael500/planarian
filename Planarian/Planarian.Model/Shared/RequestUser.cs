using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;

namespace Planarian.Model.Shared;

public class RequestUser
{
    private readonly PlanarianDbContext _dbContext;

    public RequestUser(PlanarianDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";

    public async Task Initialize(string userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(e => e.Id == userId);
        if (user == null) throw new ArgumentOutOfRangeException(nameof(user));
        Id = user.Id;
        FirstName = user.FirstName;
        LastName = user.LastName;
    }
}