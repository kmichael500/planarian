using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Trips.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Services;

public class TripService : ServiceBase<TripRepository>
{
    public TripService(TripRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    #region Trip

    public async Task<TripVm> CreateOrUpdateTrip(CreateOrEditTripVm values)
    {
        var isNew = string.IsNullOrWhiteSpace(values.Id);
        var trip = values.Id != null
            ? await Repository.GetTrip(values.Id) ?? new Trip()
            : new Trip();

        trip.ProjectId = values.ProjectId;
        trip.Name = values.Name;
        trip.TripDate = values.TripDate;

        if (isNew) Repository.Add(trip);
        await Repository.SaveChangesAsync();

        var tripNumber = await Repository.GetTripNumber(trip.Id);
        return new TripVm(trip, tripNumber);
    }

    public async Task<TripVm?> GetTrip(string tripId)
    {
        return await Repository.GetTripVm(tripId);
    }

    public async Task<object?> GetTripsByProjectId(string projectId)
    {
        return await Repository.GetTripsByProjectId(projectId);
    }

    public async Task DeleteTrip(string tripId)
    {
        var trip = await Repository.GetTrip(tripId);
        if (trip != null)
        {
            Repository.Delete(trip);
            await Repository.SaveChangesAsync();
        }
    }
    
    public async Task UpdateTripDate(DateTime date, string tripId)
    {
        var trip = await Repository.GetTrip(tripId);
        if (trip == null)
        {
            throw new NullReferenceException("Trip not found");
        }

        trip.TripDate = date;

        await Repository.SaveChangesAsync();
    }

    public async Task UpdateTripName(string name, string tripId)
    {
        var trip = await Repository.GetTrip(tripId);
        if (trip == null)
        {
            throw new NullReferenceException("Trip not found");
        }

        trip.Name = name;

        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Trip Objectives

    public async Task<IEnumerable<TripObjectiveVm>> GetTripObjectives(string tripId)
    {
        var tripObjectives = await Repository.GetTripObjectives(tripId);

        return tripObjectives;
    }

    #endregion


    #region Trip Members

    public async Task<IEnumerable<SelectListItem<string>>> GetTripMembers(string tripId)
    {
        var tripMembers = await Repository.GetTripMembers(tripId);

        return tripMembers;
    }

    #endregion
    
}