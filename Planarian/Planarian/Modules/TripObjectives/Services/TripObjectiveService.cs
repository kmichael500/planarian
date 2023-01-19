using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.Project.Controllers;
using Planarian.Modules.TripObjectives.Controllers;
using Planarian.Modules.TripObjectives.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.TripObjectives.Services;

public class TripObjectiveService : ServiceBase<TripObjectiveRepository>
{
    private readonly BlobService _blobService;
    private readonly TagRepository _tagRepository;
    private readonly UserRepository _userRepository;

    public TripObjectiveService(TripObjectiveRepository repository, RequestUser requestUser, BlobService blobService,
        TagRepository tagRepository, UserRepository userRepository) :
        base(repository, requestUser)
    {
        _blobService = blobService;
        _tagRepository = tagRepository;
        _userRepository = userRepository;
    }

    #region Photos

    public async Task UploadPhotos(IEnumerable<TripPhotoUpload> photos, string tripObjectiveId)
    {
        var ids = await Repository.GetIds(tripObjectiveId);
        if (ids == null) throw new ArgumentNullException(nameof(tripObjectiveId));

        foreach (var photo in photos)
        {
            var title = !string.IsNullOrWhiteSpace(photo.Title) ? photo.Title : photo.File.FileName;

            var fileType = Path.GetExtension(photo.File.FileName);

            if (!FileValidation.IsValidPhotoFileType(fileType)) continue;
            // TODO alert user
            var entity = new TripPhoto(RequestUser.Id, tripObjectiveId, title, photo.Description, fileType);
            Repository.Add(entity);
            await Repository.SaveChangesAsync();
            var blobKey = await _blobService.AddTripPhoto(ids.ProjectId, ids.TripId, tripObjectiveId, entity.Id,
                photo.File.OpenReadStream(), fileType);
            entity.BlobKey = blobKey;
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    public async Task<IEnumerable<SelectListItem<string>>> GetTripObjectiveMembers(string tripObjectiveId)
    {
        var tripObjectiveMembers = await Repository.GetTripObjectiveMembers(tripObjectiveId);

        return tripObjectiveMembers;
    }

    public async Task AddOrUpdateTripReport(string tripObjectiveId, string tripReport)
    {
        var tripObjective = await Repository.GetTripObjective(tripObjectiveId);

        if (tripObjective == null) throw new NullReferenceException("Trip objective does not exist");

        tripObjective.TripReport = tripReport;
        await Repository.SaveChangesAsync();
    }

    public async Task<IEnumerable<TripPhotoVm>> GetPhotos(string tripObjectiveId)
    {
        var photos = (await Repository.GetTripObjectivePhotos(tripObjectiveId)).ToList();

        foreach (var photo in photos)
        {
            var uri = _blobService.GetSasUrl(photo.Url, 24);
            if (uri?.AbsolutePath != null) photo.Url = uri.AbsoluteUri;
        }

        return photos;
    }

    public async Task UpdateObjectiveName(string tripObjectiveId, string name)
    {
        var entity = await Repository.GetTripObjective(tripObjectiveId);

        if (entity == null) throw new NullReferenceException("Trip objective does not exist");

        entity.Name = name;
        await Repository.SaveChangesAsync();
    }

    public async Task UpdateObjectiveDescription(string tripObjectiveId, string description)
    {
        var entity = await Repository.GetTripObjective(tripObjectiveId);

        if (entity == null) throw new NullReferenceException("Trip objective does not exist");

        entity.Description = description;
        await Repository.SaveChangesAsync();
    }


    public async Task InviteTripObjectiveMember(string tripObjectiveId, InviteMember invitation)
    {
        var tripObjective = await Repository.GetTripObjective(tripObjectiveId);

        if (tripObjective == null) throw new NullReferenceException("Trip objective not found");

        var user = await _userRepository.GetUserByEmail(invitation.Email);
        if (user != null)
        {
            await AddTripObjectiveMember(tripObjectiveId, user.Id);
        }
        else
        {
            var entity = new User(invitation.FirstName, invitation.LastName, invitation.Email);
            _userRepository.Add(entity);
            await Repository.SaveChangesAsync();
            await AddTripObjectiveMember(tripObjectiveId, entity.Id);
        }
    }

    #region Project

    public async Task<TripObjectiveVm> CreateOrUpdateTripObjective(CreateOrEditTripObjectiveVm values)
    {
        var isNew = string.IsNullOrWhiteSpace(values.Id);
        var tripObjective = values.Id != null
            ? await Repository.GetTripObjective(values.Id) ?? new TripObjective()
            : new TripObjective();

        tripObjective.TripId = values.TripId;

        foreach (var tripObjectiveTypeId in values.TripObjectiveTypeIds)
        {
            var tripObjectiveType = Repository.GetTripObjectiveType(tripObjectiveTypeId);
            if (tripObjectiveType == null) throw new NullReferenceException("Tag not found");

            var tripObjectiveTag = new TripObjectiveTag
            {
                Tag = tripObjectiveType
            };
            tripObjective.TripObjectiveTags.Add(tripObjectiveTag);
        }

        tripObjective.Name = values.Name;
        tripObjective.Description = values.Description;
        tripObjective.TripReport = values.TripReport;

        if (isNew)
        {
            Repository.Add(tripObjective);
            await Repository.SaveChangesAsync();
        }

        foreach (var tripObjectiveMemberId in values.TripObjectiveMemberIds)
            await AddTripObjectiveMember(tripObjective.Id, tripObjectiveMemberId, false);

        await Repository.SaveChangesAsync();
        return new TripObjectiveVm(tripObjective, values.TripObjectiveTypeIds, values.TripObjectiveMemberIds);
    }

    public async Task<TripObjectiveVm?> GetTripObjective(string projectId)
    {
        return await Repository.GetTripObjectiveVm(projectId);
    }

    public async Task DeleteTripObjective(string projectId)
    {
        var tripObjective = await Repository.GetTripObjective(projectId);
        if (tripObjective != null)
        {
            Repository.Delete(tripObjective);
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    #region Trip Objective Member

    public async Task AddTripObjectiveMember(string tripObjectiveId, string userId, bool saveChanges = true)
    {
        await AddTripObjectiveMember(tripObjectiveId, new List<string> { userId }, saveChanges);
    }

    public async Task AddTripObjectiveMember(string tripObjectiveId, IEnumerable<string> userIds,
        bool saveChanges = true)
    {
        var tripObjective = await Repository.GetTripObjective(tripObjectiveId);
        if (tripObjective == null) throw new NullReferenceException("Trip Objective not found");

        foreach (var userId in userIds)
        {
            var tripObjectiveMember = new TripObjectiveMember
            {
                TripObjective = tripObjective,
                UserId = userId
            };
            tripObjective.TripObjectiveMembers.Add(tripObjectiveMember);
        }

        if (saveChanges) await Repository.SaveChangesAsync();
    }

    public async Task DeleteTripObjectiveMember(string tripObjectiveId, string userId)
    {
        var projectMember = await Repository.GetTripObjectiveMember(tripObjectiveId, userId);
        if (projectMember != null)
        {
            Repository.Delete(projectMember);
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    #region Leads

    public async Task<IEnumerable<LeadVm>> GetLeads(string tripObjectiveId)
    {
        return await Repository.GetLeads(tripObjectiveId);
    }

    public async Task AddLeads(IEnumerable<CreateLeadVm> leads, string tripObjectiveId)
    {
        foreach (var lead in leads)
        {
            var entity = new Lead(lead, tripObjectiveId, RequestUser.Id);
            Repository.Add(entity);
        }

        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Tags

    public async Task<IEnumerable<SelectListItem<string>>> GetTripObjectiveTags(string tripObjectiveId)
    {
        return await Repository.GetTripObjectiveTags(tripObjectiveId);
    }

    public async Task AddTagType(string tagId, string tripObjectiveId)
    {
        var tagType = await _tagRepository.GetTag(tagId);

        if (tagType == null) throw new NullReferenceException("Tag type does not exist");

        var tripObjective = await Repository.GetTripObjective(tripObjectiveId);

        if (tripObjective == null) throw new NullReferenceException("Trip objective does not exist");

        var tripObjectiveTag = new TripObjectiveTag
        {
            TripObjectiveId = tripObjectiveId,
            Tag = tagType
        };
        tripObjective.TripObjectiveTags.Add(tripObjectiveTag);

        await Repository.SaveChangesAsync();
    }

    public async Task DeleteTripObjectiveTag(string tagId, string tripObjectiveId)
    {
        var tag = await Repository.GetTripObjectiveTag(tagId, tripObjectiveId);

        if (tag == null) throw new NullReferenceException("Tag does not exist");

        Repository.Delete(tag);

        await Repository.SaveChangesAsync();
    }

    #endregion
}