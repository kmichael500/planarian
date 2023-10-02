using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Shared.Exceptions;

namespace Planarian.Model.Shared;

public class RequestUser
{
    private readonly PlanarianDbContext _dbContext;

    public RequestUser(PlanarianDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Id { get; set; } = null!;
    public string? AccountId { get; set; }
    public string? UserGroupPrefix { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsAuthenticated { get; private set; }

    public async Task Initialize(string? accountId, string? userId)
    {
        var user = await _dbContext.Users.Include(e => e.AccountUsers).FirstOrDefaultAsync(e => e.Id == userId);
        if (user == null)
        {
            IsAuthenticated = false;
            return;
        }
        var isValidAccountId = user.AccountUsers.Select(e => e.AccountId).Contains(accountId);

        if (!isValidAccountId && !string.IsNullOrWhiteSpace(accountId))
        {
            throw ApiExceptionDictionary.Unauthorized("the accountId doesn't exist or is invalid for this user");
        }

        Id = user.Id;
        FirstName = user.FirstName;
        LastName = user.LastName;
        IsAuthenticated = true;

        
        if (isValidAccountId)
        {
            AccountId = accountId;
            UserGroupPrefix = $"{userId}-{AccountId}";
        }
    }

    public string AccountContainerName =>
        $"account-{AccountId?.ToLower() ?? throw new NullReferenceException($" {nameof(AccountId)} is null")}";
}