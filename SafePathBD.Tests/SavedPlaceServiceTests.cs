using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.SavedPlaces;
using SafePathBD.Web.Services.Implementations;

namespace SafePathBD.Tests;

public sealed class SavedPlaceServiceTests
{
    [Fact]
    public async Task SavingHomeAgain_UpdatesSameShortcutInsteadOfCreatingDuplicate()
    {
        await using var db = CreateDb();
        var service = new SavedPlaceService(db, new LocationService(db));

        var first = await service.SaveAsync(7, new SavePlaceRequest(
            "Ignored", "HOME", 23.76361, 90.40694, "First home"));
        var second = await service.SaveAsync(7, new SavePlaceRequest(
            "Ignored", "HOME", 23.77000, 90.41000, "New home"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Single(await db.SavedPlaces.Where(x => x.UserId == 7).ToListAsync());
        var item = await db.SavedPlaces.Include(x => x.Location).SingleAsync();
        Assert.Equal("Home", item.PlaceName);
        Assert.Equal("HOME", item.PlaceType);
        Assert.Equal(23.77m, decimal.Round(item.Location.Latitude, 2));
    }

    [Fact]
    public async Task GetForUser_ReturnsOnlyOwnersPlaces()
    {
        await using var db = CreateDb();
        var service = new SavedPlaceService(db, new LocationService(db));
        await service.SaveAsync(1, new SavePlaceRequest("Gym", "FAVORITE", 23.75, 90.40));
        await service.SaveAsync(2, new SavePlaceRequest("Cafe", "FAVORITE", 23.76, 90.41));

        var places = await service.GetForUserAsync(1);

        Assert.Single(places);
        Assert.Equal("Gym", places[0].PlaceName);
    }

    [Fact]
    public async Task Delete_DoesNotAllowAnotherUserToRemovePlace()
    {
        await using var db = CreateDb();
        var service = new SavedPlaceService(db, new LocationService(db));
        var saved = await service.SaveAsync(1, new SavePlaceRequest("Gym", "FAVORITE", 23.75, 90.40));

        var deleted = await service.DeleteAsync(2, saved.Place!.SavedPlaceId);

        Assert.False(deleted);
        Assert.Equal(1, await db.SavedPlaces.CountAsync());
    }

    private static SafePathDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SafePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SafePathDbContext(options);
    }
}
