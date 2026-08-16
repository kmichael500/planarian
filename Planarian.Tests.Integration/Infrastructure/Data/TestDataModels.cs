using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal sealed record AccountCountyTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId);

internal sealed record CaveTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId,
    string CaveId,
    string CaveName,
    int CountyNumber);

internal sealed record PublishedCaveTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId,
    string CaveId,
    string CaveName,
    int CountyNumber,
    string RevisionId);

internal sealed record TestFileData(string FileId, string FileTypeId);
internal sealed record PendingChangeRequestTestData(string ChangeRequestId, string? ProposalVersionId = null);
internal sealed record PendingReviewWithStagedFileTestData(
    string ChangeRequestId,
    string ProposalVersionId,
    string FileId,
    string StagedFileId);
