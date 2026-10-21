using CarService.Core.Entities;

namespace CarService.Tests;

// Pure unit tests: no database, no web server. Possible because the rules live in Core.
public class ServiceAppointmentTests
{
    private static ServiceAppointment New() => new() { Description = "Oil change" };

    [Fact]
    public void NewAppointment_StartsScheduled()
    {
        Assert.Equal(AppointmentStatus.Scheduled, New().Status);
    }

    [Fact]
    public void Scheduled_CannotJumpStraightToCompleted()
    {
        var a = New();

        var result = a.ChangeStatus(AppointmentStatus.Completed, cost: 100);

        Assert.False(result.Succeeded);
        Assert.Equal(AppointmentStatus.Scheduled, a.Status);   // unchanged on failure
    }

    [Fact]
    public void Completing_RequiresACost()
    {
        var a = New();
        a.ChangeStatus(AppointmentStatus.InProgress);

        var result = a.ChangeStatus(AppointmentStatus.Completed);

        Assert.False(result.Succeeded);
        Assert.Equal(AppointmentStatus.InProgress, a.Status);
    }

    [Fact]
    public void HappyPath_ScheduledToInProgressToCompleted()
    {
        var a = New();

        Assert.True(a.ChangeStatus(AppointmentStatus.InProgress).Succeeded);
        Assert.True(a.ChangeStatus(AppointmentStatus.Completed, cost: 149.90m).Succeeded);

        Assert.Equal(AppointmentStatus.Completed, a.Status);
        Assert.Equal(149.90m, a.Cost);
        Assert.True(a.IsClosed);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void ClosedAppointments_AllowNoFurtherChanges(AppointmentStatus final)
    {
        var a = New();
        a.Status = final;

        Assert.Empty(a.AllowedNextStatuses);
        Assert.False(a.ChangeStatus(AppointmentStatus.Scheduled).Succeeded);
    }
}
