using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Planarian.Model.Database.Extensions;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Interceptors;

/// <summary>
/// Validates Cave/County/Entrance/File ownership once per distinct foreign-key chunk
/// before SaveChanges validation. Both current and original parent IDs are checked
/// so moving or severing a relationship cannot hide its pre-save owner.
/// </summary>
internal static class SaveChangesOwnershipBatchLoader
{
    private const int LookupBatchSize = 500;

    internal sealed record InvitationClaim(string ClaimingUserId, string TemporaryUserId);

    internal sealed class ValidationResult
    {
        private readonly HashSet<object> _validatedParentRelationships =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, InvitationClaim> _invitationClaims =
            new(StringComparer.Ordinal);
        private readonly HashSet<object> _addedTemporaryInvitationUsers =
            new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> _deletedTemporaryInvitationUsers =
            new(ReferenceEqualityComparer.Instance);

        internal void MarkValidated(IEnumerable<OwnershipLink> links)
        {
            foreach (var link in links)
                _validatedParentRelationships.Add(link.Entity);
        }

        internal void SetInvitationClaims(IReadOnlyDictionary<string, InvitationClaim> claims)
        {
            foreach (var (accountId, claim) in claims)
                _invitationClaims[accountId] = claim;
        }

        internal bool HasValidatedParentRelationship(object entity) =>
            _validatedParentRelationships.Contains(entity);

        internal bool IsInvitationClaim(string? accountId, string? claimingUserId) =>
            !string.IsNullOrWhiteSpace(accountId) &&
            !string.IsNullOrWhiteSpace(claimingUserId) &&
            _invitationClaims.TryGetValue(accountId, out var claim) &&
            claim.ClaimingUserId == claimingUserId;

        internal bool IsInvitationPermissionTransfer(
            string? accountId,
            string? claimingUserId,
            string? originalUserId) =>
            IsInvitationClaim(accountId, claimingUserId) &&
            !string.IsNullOrWhiteSpace(originalUserId) &&
            _invitationClaims[accountId!].TemporaryUserId == originalUserId;

        internal void MarkTemporaryInvitationUser(User user, EntityState state)
        {
            if (state == EntityState.Added) _addedTemporaryInvitationUsers.Add(user);
            if (state == EntityState.Deleted) _deletedTemporaryInvitationUsers.Add(user);
        }

        internal bool IsTemporaryInvitationUserTransition(User user, EntityState state) =>
            state == EntityState.Added
                ? _addedTemporaryInvitationUsers.Contains(user)
                : state == EntityState.Deleted && _deletedTemporaryInvitationUsers.Contains(user);
    }

    internal sealed record OwnershipLink(
        object Entity,
        IReadOnlyList<string> ValidationIds,
        string? OwnerAccountId);

    private sealed record InvitationCandidate(
        string AccountId,
        string ClaimingUserId,
        string NormalizedEmail,
        DateTime? InvitationSentOn);

    public static async Task<ValidationResult> ValidateAsync(PlanarianDbContextBase context,
        IReadOnlyList<EntityEntry> entries, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        var changed = entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changed.Count == 0) return result;

        var invitationClaims = await LoadInvitationClaimsAsync(context, changed, cancellationToken);
        result.SetInvitationClaims(invitationClaims);
        MarkTemporaryInvitationUserTransitions(changed, result);

        var requestAccountId = context.RequestUser?.AccountId;
        var caveLinks = new List<OwnershipLink>();
        var countyLinks = new List<OwnershipLink>();
        var entranceLinks = new List<OwnershipLink>();
        var stagedFileIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in changed)
        {
            var ownerAccountId = ExpectedParentAccountId(entry.Entity, requestAccountId, invitationClaims);
            AddOwnershipLinks(entry, typeof(Cave), ownerAccountId, caveLinks);
            AddOwnershipLinks(entry, typeof(County), ownerAccountId, countyLinks);
            AddOwnershipLinks(entry, typeof(Entrance), ownerAccountId, entranceLinks);

            if (entry.Entity is CaveChangeRequestStagedFile)
                foreach (var id in CurrentAndOriginalIds(entry,
                             entry.Metadata.FindProperty(nameof(CaveChangeRequestStagedFile.FileId))
                             ?? throw new InvalidOperationException("Staged-file ownership metadata is missing FileId.")))
                    stagedFileIds.Add(id);
        }

        await ValidateCaveLinksAsync(context, entries, caveLinks, result, cancellationToken);
        await ValidateCountyLinksAsync(context, entries, countyLinks, result, cancellationToken);
        await ValidateEntranceLinksAsync(context, entries, caveLinks, entranceLinks, result, cancellationToken);

        if (stagedFileIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(requestAccountId))
                throw Forbidden();

            var ownedFileIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chunk in stagedFileIds.Chunk(LookupBatchSize))
            {
                var ids = await context.Files.IgnoreQueryFilters()
                    .Where(f => chunk.Contains(f.Id) &&
                                (f.AccountId == requestAccountId ||
                                 (f.Cave != null && f.Cave.AccountId == requestAccountId)))
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken);
                ownedFileIds.UnionWith(ids);
            }

            if (stagedFileIds.Any(id => !ownedFileIds.Contains(id)))
                throw Forbidden();
        }

        return result;
    }

    private static void MarkTemporaryInvitationUserTransitions(
        IReadOnlyCollection<EntityEntry> changed,
        ValidationResult result)
    {
        var changedTemporaryUsers = changed
            .Where(entry => entry.Entity is User { IsTemporary: true })
            .Select(entry => (Entry: entry, User: (User)entry.Entity))
            .ToList();

        foreach (var membershipEntry in changed.Where(entry => entry.Entity is AccountUser))
        {
            if (membershipEntry.State is not (EntityState.Added or EntityState.Deleted)) continue;
            var membership = (AccountUser)membershipEntry.Entity;
            if (string.IsNullOrWhiteSpace(membership.InvitationCode)) continue;

            User? temporaryUser = membership.User is { IsTemporary: true } navigationUser
                ? navigationUser
                : changedTemporaryUsers
                    .Where(item => item.Entry.State == membershipEntry.State)
                    .Select(item => item.User)
                    .FirstOrDefault(user => !string.IsNullOrWhiteSpace(user.Id) && user.Id == membership.UserId);

            if (temporaryUser is not null)
                result.MarkTemporaryInvitationUser(temporaryUser, membershipEntry.State);
        }
    }

    private static async Task<Dictionary<string, InvitationClaim>> LoadInvitationClaimsAsync(
        PlanarianDbContextBase context,
        IReadOnlyCollection<EntityEntry> changed,
        CancellationToken cancellationToken)
    {
        var addedMemberships = changed
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .OfType<AccountUser>()
            .Where(accountUser =>
                accountUser.InvitationAcceptedOn != null &&
                !string.IsNullOrWhiteSpace(accountUser.AccountId) &&
                !string.IsNullOrWhiteSpace(accountUser.UserId))
            .ToList();
        if (addedMemberships.Count == 0)
            return new Dictionary<string, InvitationClaim>(StringComparer.Ordinal);

        var requestUserId = context.RequestUser?.Id;
        if (!string.IsNullOrWhiteSpace(requestUserId))
            addedMemberships = addedMemberships
                .Where(accountUser => accountUser.UserId == requestUserId)
                .ToList();
        if (addedMemberships.Count == 0)
            return new Dictionary<string, InvitationClaim>(StringComparer.Ordinal);

        var claimantUserIds = addedMemberships
            .Select(accountUser => accountUser.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var claimantEmails = changed
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .OfType<User>()
            .Where(user =>
                !user.IsTemporary &&
                claimantUserIds.Contains(user.Id) &&
                !string.IsNullOrWhiteSpace(user.EmailAddress))
            .GroupBy(user => user.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Single().EmailAddress.ToLowerInvariant(),
                StringComparer.Ordinal);

        var missingClaimantUserIds = claimantUserIds
            .Where(userId => !claimantEmails.ContainsKey(userId))
            .ToArray();
        if (missingClaimantUserIds.Length > 0)
        {
            var persistedEmails = await context.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user =>
                    missingClaimantUserIds.Contains(user.Id) &&
                    !user.IsTemporary)
                .Select(user => new { user.Id, user.EmailAddress })
                .ToListAsync(cancellationToken);
            foreach (var user in persistedEmails)
                claimantEmails[user.Id] = user.EmailAddress.ToLowerInvariant();
        }

        var candidates = addedMemberships
            .Where(accountUser => claimantEmails.ContainsKey(accountUser.UserId))
            .Select(accountUser => new InvitationCandidate(
                accountUser.AccountId,
                accountUser.UserId,
                claimantEmails[accountUser.UserId],
                accountUser.InvitationSentOn))
            .GroupBy(candidate => candidate.AccountId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        if (candidates.Count == 0)
            return new Dictionary<string, InvitationClaim>(StringComparer.Ordinal);

        var candidateAccountIds = candidates.Keys.ToArray();
        var pendingInvitations = await context.AccountUsers.IgnoreQueryFilters()
            .Where(accountUser =>
                candidateAccountIds.Contains(accountUser.AccountId) &&
                accountUser.InvitationCode != null &&
                accountUser.InvitationAcceptedOn == null &&
                accountUser.User != null &&
                accountUser.User.IsTemporary)
            .Select(accountUser => new
            {
                accountUser.AccountId,
                accountUser.UserId,
                accountUser.InvitationSentOn,
                NormalizedEmail = accountUser.User!.EmailAddress.ToLower()
            })
            .ToListAsync(cancellationToken);

        return pendingInvitations
            .Where(invitation =>
                candidates.TryGetValue(invitation.AccountId, out var candidate) &&
                invitation.InvitationSentOn == candidate.InvitationSentOn &&
                invitation.NormalizedEmail == candidate.NormalizedEmail)
            .GroupBy(invitation => invitation.AccountId, StringComparer.Ordinal)
            .Where(group => group.Select(invitation => invitation.UserId)
                .Distinct(StringComparer.Ordinal)
                .Count() == 1)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var candidate = candidates[group.Key];
                    return new InvitationClaim(candidate.ClaimingUserId, group.First().UserId);
                },
                StringComparer.Ordinal);
    }

    private static string? ExpectedParentAccountId(
        object entity,
        string? requestAccountId,
        IReadOnlyDictionary<string, InvitationClaim> invitationClaims)
    {
        if (entity is CavePermission cavePermission &&
            invitationClaims.ContainsKey(cavePermission.AccountId))
            return cavePermission.AccountId;

        return requestAccountId;
    }

    private static async Task ValidateCaveLinksAsync(
        PlanarianDbContextBase context,
        IReadOnlyList<EntityEntry> entries,
        IReadOnlyCollection<OwnershipLink> links,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        foreach (var group in links.GroupBy(link => link.OwnerAccountId, StringComparer.Ordinal))
        {
            var accountId = RequireAccountId(group.Key);
            var addedIds = entries
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .OfType<Cave>()
                .Where(cave => cave.AccountId == accountId && !string.IsNullOrWhiteSpace(cave.Id))
                .Select(cave => cave.Id)
                .ToHashSet(StringComparer.Ordinal);

            var ownedIds = await LoadOwnedIdsAsync(
                group,
                addedIds,
                async chunk => await context.Caves.IgnoreQueryFilters()
                    .Where(cave => cave.AccountId == accountId && chunk.Contains(cave.Id))
                    .Select(cave => cave.Id)
                    .ToListAsync(cancellationToken));

            EnsureAllOwned(group, ownedIds);
            result.MarkValidated(group);
        }
    }

    private static async Task ValidateCountyLinksAsync(
        PlanarianDbContextBase context,
        IReadOnlyList<EntityEntry> entries,
        IReadOnlyCollection<OwnershipLink> links,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        foreach (var group in links.GroupBy(link => link.OwnerAccountId, StringComparer.Ordinal))
        {
            var accountId = RequireAccountId(group.Key);
            var addedIds = entries
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .OfType<County>()
                .Where(county => county.AccountId == accountId && !string.IsNullOrWhiteSpace(county.Id))
                .Select(county => county.Id)
                .ToHashSet(StringComparer.Ordinal);

            var ownedIds = await LoadOwnedIdsAsync(
                group,
                addedIds,
                async chunk => await context.Counties.IgnoreQueryFilters()
                    .Where(county => county.AccountId == accountId && chunk.Contains(county.Id))
                    .Select(county => county.Id)
                    .ToListAsync(cancellationToken));

            EnsureAllOwned(group, ownedIds);
            result.MarkValidated(group);
        }
    }

    private static async Task ValidateEntranceLinksAsync(
        PlanarianDbContextBase context,
        IReadOnlyList<EntityEntry> entries,
        IReadOnlyCollection<OwnershipLink> caveLinks,
        IReadOnlyCollection<OwnershipLink> links,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        foreach (var group in links.GroupBy(link => link.OwnerAccountId, StringComparer.Ordinal))
        {
            var accountId = RequireAccountId(group.Key);
            var addedIds = entries
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .OfType<Entrance>()
                .Where(entrance => !string.IsNullOrWhiteSpace(entrance.Id) &&
                                   caveLinks.Any(link =>
                                       ReferenceEquals(link.Entity, entrance) &&
                                       link.OwnerAccountId == accountId))
                .Select(entrance => entrance.Id)
                .ToHashSet(StringComparer.Ordinal);

            var ownedIds = await LoadOwnedIdsAsync(
                group,
                addedIds,
                async chunk => await context.Entrances.IgnoreQueryFilters()
                    .Where(entrance =>
                        chunk.Contains(entrance.Id) &&
                        entrance.Cave != null &&
                        entrance.Cave.AccountId == accountId)
                    .Select(entrance => entrance.Id)
                    .ToListAsync(cancellationToken));

            EnsureAllOwned(group, ownedIds);
            result.MarkValidated(group);
        }
    }

    private static async Task<HashSet<string>> LoadOwnedIdsAsync(
        IEnumerable<OwnershipLink> links,
        HashSet<string> addedIds,
        Func<string[], Task<List<string>>> loadChunkAsync)
    {
        var ownedIds = new HashSet<string>(addedIds, StringComparer.Ordinal);
        var requiredIds = links
            .SelectMany(link => link.ValidationIds)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !ownedIds.Contains(id))
            .ToList();

        foreach (var chunk in requiredIds.Chunk(LookupBatchSize))
            ownedIds.UnionWith(await loadChunkAsync(chunk));

        return ownedIds;
    }

    private static void AddOwnershipLinks(
        EntityEntry entry,
        Type principalType,
        string? ownerAccountId,
        ICollection<OwnershipLink> links)
    {
        foreach (var reference in entry.Metadata.GetReferencesTo(principalType))
        {
            var property = reference.GetDependentPropertyForPrincipalProperty<string>(nameof(EntityBase.Id));

            // A Cave is directly account-owned, and SaveChanges separately validates both its current
            // and original AccountId. Rechecking an unchanged County relationship on every unrelated
            // Cave update adds one ownership SELECT per bulk-revision batch without protecting a
            // relationship transition. New Caves and actual County moves still validate it.
            if (entry.Entity is Cave && principalType == typeof(County) &&
                entry.State == EntityState.Modified && !entry.Property(property.Name).IsModified)
                continue;

            var validationIds = CurrentAndOriginalIds(entry, property);
            if (validationIds.Count > 0)
                links.Add(new OwnershipLink(entry.Entity, validationIds, ownerAccountId));
        }
    }

    private static IReadOnlyList<string> CurrentAndOriginalIds(EntityEntry entry, IProperty propertyMetadata)
    {
        var property = entry.Property(propertyMetadata.Name);
        var currentId = property.CurrentValue as string;
        string? originalId = null;
        if (entry.State is EntityState.Modified or EntityState.Deleted)
            originalId = property.OriginalValue as string;

        return new[] { currentId, originalId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequireAccountId(string? accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw Forbidden();
        return accountId;
    }

    private static void EnsureAllOwned(IEnumerable<OwnershipLink> links, IReadOnlySet<string> ownedIds)
    {
        if (links.SelectMany(link => link.ValidationIds).Any(id => !ownedIds.Contains(id)))
            throw Forbidden();
    }

    private static ApiException Forbidden() =>
        ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
}
