using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Modules.Caves.Services;

public class ChangeLogBuilder
{
    private readonly List<CaveChangeHistory> _changes = [];
    private readonly string _accountId;
    private readonly string? _caveId;
    private readonly string _changedByUserId;
    private readonly string _approvedByUserId;
    private readonly string _changeRequestId;
    
    private readonly DateTime _now = DateTime.UtcNow;

    public ChangeLogBuilder(string accountId, string? caveId, string changedByUserId, string approvedByUserId,
        string changeRequestId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw ApiExceptionDictionary.NoAccount;

        _accountId = accountId;
        _caveId = caveId;
        _changedByUserId = changedByUserId;
        _approvedByUserId = approvedByUserId;
        _changeRequestId = changeRequestId;
    }

    public void AddStringFieldAsync(string propertyName, string? original, string? current, string? entranceId = null)
    {
        if (string.Equals(original, current, StringComparison.InvariantCulture))
            return;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.String,
            valueString: current,
            originalValueString: original,
            entranceId: entranceId
        ));
    }

    public void AddIntFieldAsync(string propertyName, int? original, int? current, string? entranceId = null)
    {
        if (original.Equals(current))
            return;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.Int,
            valueInt: current,
            originalValueInt: original,
            entranceId: entranceId
        ));
    }

    public void AddDoubleFieldAsync(string propertyName, double? original, double? current, string? entranceId = null)
    {
        if (original.Equals(current))
            return;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.Double,
            valueDouble: current,
            originalValueDouble: original,
            entranceId: entranceId
        ));
    }

    public void AddBoolFieldAsync(string propertyName, bool? original, bool? current, string? entranceId = null)
    {
        if (original.Equals(current))
            return;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.Bool,
            valueBool: current,
            originalValueBool: original,
            entranceId: entranceId
        ));
    }

    public void AddDateTimeFieldAsync(string propertyName, DateTime? original, DateTime? current,
        string? entranceId = null)
    {
        if (original.Equals(current))
            return;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.DateTime,
            valueDateTime: current,
            originalValueDateTime: original,
            entranceId: entranceId
        ));
    }

    public void AddArrayFieldAsync(string propertyName, IEnumerable<string>? original, IEnumerable<string>? current,
        string? entranceId = null)
    {
        var (added, removed) = DiffStringArrays(original ?? [],
            current ?? []);

        foreach (var val in added)
            _changes.Add(CreateLog(
                propertyName,
                ChangeValueType.String,
                valueString: val,
                entranceId: entranceId
            ));

        foreach (var val in removed)
            _changes.Add(CreateLog(
                propertyName,
                ChangeValueType.String,
                originalValueString: val,
                entranceId: entranceId
            ));
    }

    public async Task AddNamedIdFieldAsync(string propertyName,
        string? originalId,
        string? currentId,
        Func<string, Task<string?>> lookup, string? entranceId = null)
    {
        if (string.Equals(originalId, currentId, StringComparison.Ordinal))
            return;

        var origName = originalId != null ? await InternalLookup(originalId, lookup) : null;
        var newName = currentId != null ? await InternalLookup(currentId, lookup) : null;

        _changes.Add(CreateLog(
            propertyName,
            ChangeValueType.String,
            valueString: newName,
            originalValueString: origName,
            propertyId: currentId, entranceId: entranceId));
    }

    public async Task AddNamedArrayFieldAsync(string propertyName,
        IEnumerable<string>? originalIds,
        IEnumerable<string>? currentIds,
        Func<string, Task<string?>> lookup, string? entranceId = null, string? overrideCaveId = null)
    {
        var (added, removed) = DiffStringArrays(originalIds ?? [], currentIds ?? []);


        foreach (var id in added)
        {
            var name = await InternalLookup(id, lookup);

            _changes.Add(CreateLog(
                propertyName,
                ChangeValueType.String,
                valueString: name,
                propertyId: id, entranceId: entranceId, overrideCaveId: overrideCaveId
                )
            );
        }

        foreach (var id in removed)
        {
            var name = await InternalLookup(id, lookup);
            _changes.Add(CreateLog(
                propertyName,
                ChangeValueType.String,
                originalValueString: name,
                propertyId: id, entranceId: entranceId, overrideCaveId: overrideCaveId
                )
            );
        }
    }

    private CaveChangeHistory CreateLog(string propertyName,
        string changeValueType,
        string? valueString = null,
        int? valueInt = null,
        double? valueDouble = null,
        DateTime? valueDateTime = null,
        bool? valueBool = null,
        string? originalValueString = null,
        int? originalValueInt = null,
        double? originalValueDouble = null,
        DateTime? originalValueDateTime = null,
        bool? originalValueBool = null,
        string? propertyId = null,
        string? entranceId = null, string? changeType = null, string? overrideCaveId = null)
    { 
        changeType ??= DetermineChangeType(
            original: changeValueType switch
            {
                ChangeValueType.String => (object?)originalValueString,
                ChangeValueType.Int => originalValueInt,
                ChangeValueType.Double => originalValueDouble,
                ChangeValueType.DateTime => originalValueDateTime,
                ChangeValueType.Bool => originalValueBool,
                _ => null
            },
            current: changeValueType switch
            {
                ChangeValueType.String => (object?)valueString,
                ChangeValueType.Int => valueInt,
                ChangeValueType.Double => valueDouble,
                ChangeValueType.DateTime => valueDateTime,
                ChangeValueType.Bool => valueBool,
                _ => null
            });

        var record = new CaveChangeHistory
        {
            AccountId = _accountId,
            CaveId = overrideCaveId ?? _caveId,
            EntranceId = entranceId,
            ChangedByUserId = _changedByUserId,
            ApprovedByUserId = _approvedByUserId,
            CaveChangeRequestId = _changeRequestId,
            PropertyName = propertyName,
            ChangeValueType = changeValueType,
            ValueString = valueString,
            ValueInt = valueInt,
            ValueDouble = valueDouble,
            ValueDateTime = valueDateTime,
            ValueBool = valueBool,
            ChangeType = changeType,
            PropertyId = propertyId,
            CreatedOn = _now
        };

        if (changeValueType == ChangeValueType.Entrance && entranceId.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(entranceId),
                "EntranceId must be provided for ChangeValueType.Entrance");
        }

        return record;
    }

    private static string DetermineChangeType(object? original, object? current)
    {
        var hasOriginal = original != null;
        var hasCurrent = current != null;

        switch (hasOriginal)
        {
            case false when hasCurrent:
                return ChangeType.Add;
            case true when !hasCurrent:
                return ChangeType.Delete;
        }

        if (!Equals(original, current)) return ChangeType.Update;
        return ChangeType.Update;
    }

    private static (List<string> Added, List<string> Removed) DiffStringArrays(
        IEnumerable<string> original,
        IEnumerable<string> modified)
    {
        var before = original.ToList();
        var after = modified.ToList();
        var added = after.Except(before).ToList();
        var removed = before.Except(after).ToList();
        return (added, removed);
    }

    public List<CaveChangeHistory> Build() => _changes;


    // private lookupFunction that if the lookup function fails it will return the raw value because it is a new value

    private static async Task<string?> InternalLookup(string value, Func<string, Task<string?>> lookup)
    {
        if (!value.IsValidId()) return value;

        var lookupResult = await lookup(value);
        return lookupResult;
    }

    public void AddRemoveEntranceLog(string removedEntranceId)
    {
        _changes.Add(CreateLog(
            propertyName: CaveLogPropertyNames.Entrance,
            changeValueType: ChangeValueType.Entrance,
            entranceId: removedEntranceId,
            changeType: ChangeType.Delete
            )
        );
    }

    public void AddAddedEntranceLog(string? addedEntranceId)
    {
        _changes.Add(CreateLog(
                propertyName: CaveLogPropertyNames.Entrance,
                changeValueType: ChangeValueType.Entrance,
                entranceId: addedEntranceId, changeType: ChangeType.Add)
        );
    }

    public void AddRemovedCaveLog()
    {
        _changes.Add(CreateLog(
            propertyName: CaveLogPropertyNames.Cave,
            changeValueType: ChangeValueType.Cave,
            changeType: ChangeType.Delete
        ));
    }
    public void AddAddedCaveLog()
    {
        _changes.Add(CreateLog(
            propertyName: CaveLogPropertyNames.Cave,
            changeValueType: ChangeValueType.Cave,
            changeType: ChangeType.Add
        ));
    }
}