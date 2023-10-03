using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Invitations.Models;
using Planarian.Modules.Photos.Models;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Tags.Repositories;
using Planarian.Modules.Trips.Models;
using Planarian.Modules.Trips.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Trips.Services;

public class TripService : ServiceBase<TripRepository>
{
    private readonly BlobService _blobService;
    private readonly TagRepository _tagRepository;
    private readonly UserRepository _userRepository;

    public TripService(TripRepository repository, RequestUser requestUser, BlobService blobService,
        TagRepository tagRepository, UserRepository userRepository) :
        base(repository, requestUser)
    {
        _blobService = blobService;
        _tagRepository = tagRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripMembers(string tripId)
    {
        var tripMembers = await Repository.GetTripMembers(tripId);

        return tripMembers;
    }

    public async Task AddOrUpdateTripReport(string tripId, string tripReport)
    {
        var trip = await Repository.GetTrip(tripId);

        if (trip == null) throw new NullReferenceException("Trip does not exist");

        trip.TripReport = tripReport;
        await Repository.SaveChangesAsync();
    }

    public async Task InviteTripMember(string tripId, InviteMember invitation)
    {
        var trip = await Repository.GetTrip(tripId);

        if (trip == null) throw new NullReferenceException("Trip not found");

        var user = await _userRepository.GetUserByEmail(invitation.Email);
        if (user != null)
        {
            await AddTripMember(tripId, user.Id);
        }
        else
        {
            var entity = new User(invitation.FirstName, invitation.LastName, invitation.Email);
            _userRepository.Add(entity);
            await Repository.SaveChangesAsync();
            await AddTripMember(tripId, entity.Id);
        }
    }

    #region Photos

    public async Task<IEnumerable<PhotoVm>> GetTripPhotos(string tripId)
    {
        var photos = (await Repository.GetTripPhotos(tripId)).ToList();

        foreach (var photo in photos)
        {
            var uri = _blobService.GetSasUrl(photo.Url, 24);
            if (uri?.AbsolutePath != null) photo.Url = uri.AbsoluteUri;
        }

        return photos;
    }

    public async Task UploadTripPhotos(IEnumerable<PhotoUpload> photos, string tripId)
    {
        var ids = await Repository.GetIds(tripId);
        if (ids == null) throw new ArgumentNullException(nameof(tripId));

        foreach (var photo in photos)
        {
            var title = !string.IsNullOrWhiteSpace(photo.Title) ? photo.Title : photo.File.FileName;

            var fileType = Path.GetExtension(photo.File.FileName);

            if (!FileValidation.IsValidPhotoFileType(fileType)) continue;
            // TODO: don't throw exception but alert user
            var entity = new Photo(tripId, title, photo.Description, fileType);
            Repository.Add(entity);
            await Repository.SaveChangesAsync();
            var blobKey = await _blobService.AddTripPhoto(ids.ProjectId, ids.TripId, entity.Id,
                photo.File.OpenReadStream(), fileType);
            entity.BlobKey = blobKey;
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    #region Trip

    public async Task<TripVm> CreateOrUpdateTrip(CreateOrEditTripVm values)
    {
        var isNew = string.IsNullOrWhiteSpace(values.Id);
        var trip = values.Id != null
            ? await Repository.GetTrip(values.Id) ?? new Trip()
            : new Trip();

        trip.ProjectId = values.ProjectId;

        foreach (var tripTagId in values.TripTagTypeIds)
        {
            var tripTag = Repository.GetTripTag(tripTagId);
            if (tripTag == null) throw new NullReferenceException("Tag not found");

            var entity = new TripTag
            {
                TagType = tripTag
            };
            trip.TripTags.Add(entity);
        }

        trip.Name = values.Name;
        trip.Description = values.Description;
        trip.TripReport = values.TripReport;

        var numberOfPhotos = 0;
        if (isNew)
        {
            Repository.Add(trip);
            await Repository.SaveChangesAsync();
        }
        else
        {
            numberOfPhotos = await Repository.GetNumberOfTripPhotos(trip.Id);
        }

        foreach (var tripMemberId in values.TripMemberIds) await AddTripMember(trip.Id, tripMemberId, false);


        await Repository.SaveChangesAsync();
        return new TripVm(trip, values.TripTagTypeIds, values.TripMemberIds, numberOfPhotos);
    }

    public async Task<TripVm?> GetTrip(string projectId)
    {
        return await Repository.GetTripVm(projectId);
    }

    public async Task<PagedResult<TripVm>> GetTripsByProjectId(string projectId, FilterQuery query)
    {
        var trips = await Repository.GetTripsByProjectIdAsQueryable(projectId, query);

        return trips;
    }

    public async Task DeleteTrip(string projectId)
    {
        var trip = await Repository.GetTrip(projectId);
        if (trip != null)
        {
            Repository.Delete(trip);
            await Repository.SaveChangesAsync();
        }
    }

    public async Task UpdateTripName(string tripId, string name)
    {
        var entity = await Repository.GetTrip(tripId);

        if (entity == null) throw new NullReferenceException("Trip does not exist");

        entity.Name = name;
        await Repository.SaveChangesAsync();
    }

    public async Task UpdateTripDescription(string tripId, string description)
    {
        var entity = await Repository.GetTrip(tripId);

        if (entity == null) throw new NullReferenceException("Trip does not exist");

        entity.Description = description;
        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Trip Member

    public async Task AddTripMember(string tripId, string userId, bool saveChanges = true)
    {
        await AddTripMembers(tripId, new List<string> { userId }, saveChanges);
    }

    public async Task AddTripMembers(string tripId, IEnumerable<string> userIds,
        bool saveChanges = true)
    {
        var trip = await Repository.GetTrip(tripId);
        if (trip == null) throw new NullReferenceException("Trip not found");

        foreach (var userId in userIds)
        {
            var tripMember = new Member
            {
                Trip = trip,
                UserId = userId
            };
            trip.Members.Add(tripMember);
        }

        if (saveChanges) await Repository.SaveChangesAsync();
    }

    public async Task DeleteTripMember(string tripId, string userId)
    {
        var projectMember = await Repository.GetTripMember(tripId, userId);
        if (projectMember != null)
        {
            Repository.Delete(projectMember);
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    #region Leads

    public async Task<IEnumerable<LeadVm>> GetTripLeads(string tripId)
    {
        return await Repository.GetLeads(tripId);
    }

    public async Task AddTripLeads(IEnumerable<CreateLeadVm> leads, string tripId)
    {
        foreach (var lead in leads)
        {
            var entity = new Lead(lead, tripId, RequestUser.Id);
            Repository.Add(entity);
        }

        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Tags

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags(string tripId)
    {
        return await Repository.GetTripTags(tripId);
    }

    public async Task AddTripTag(string tagTypeId, string tripId)
    {
        var tagType = await _tagRepository.GetTag(tagTypeId);

        if (tagType == null) throw new NullReferenceException("Tag type does not exist");

        var trip = await Repository.GetTrip(tripId);

        if (trip == null) throw new NullReferenceException("Trip does not exist");

        var tripTag = new TripTag
        {
            TripId = tripId,
            TagType = tagType
        };
        trip.TripTags.Add(tripTag);

        await Repository.SaveChangesAsync();
    }

    public async Task DeleteTripTag(string tagTypeId, string tripId)
    {
        var tag = await Repository.GetTripTag(tagTypeId, tripId);

        if (tag == null) throw new NullReferenceException("Tag does not exist");

        Repository.Delete(tag);

        await Repository.SaveChangesAsync();
    }

    #endregion
}