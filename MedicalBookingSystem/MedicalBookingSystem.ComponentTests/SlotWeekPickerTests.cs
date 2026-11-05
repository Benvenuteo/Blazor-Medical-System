using Bunit;
using MedicalBookingSystem.BlazorWasm.Shared;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class SlotWeekPickerTests : BunitContext
{
    // Wednesday 4 November 2026 and Tuesday 10 November 2026: two different weeks.
    private static readonly DateTime FirstWeekSlot = new(2026, 11, 4, 9, 0, 0);
    private static readonly DateTime NextWeekSlot = new(2026, 11, 10, 14, 30, 0);
    private static readonly IReadOnlyList<AvailableSlotDto> Slots =
    [
        new() { DoctorId = 1, StartTime = FirstWeekSlot, EndTime = FirstWeekSlot.AddMinutes(30), ClinicName = "Centrum" },
        new() { DoctorId = 1, StartTime = NextWeekSlot, EndTime = NextWeekSlot.AddMinutes(30) }
    ];

    [Fact]
    public void StartsAtTheWeekOfTheFirstSlot_AndNavigatesBetweenWeeks()
    {
        var picker = Render<SlotWeekPicker>(p => p.Add(c => c.Slots, Slots));

        Assert.Contains("02.11 – 08.11.2026", picker.Markup);
        Assert.Contains("09:00", picker.Markup);
        Assert.DoesNotContain("14:30", picker.Markup);
        Assert.True(picker.FindAll("button").Single(b => b.TextContent.Contains("Poprzedni")).HasAttribute("disabled"));

        picker.FindAll("button").Single(b => b.TextContent.Contains("Następny")).Click();

        Assert.Contains("09.11 – 15.11.2026", picker.Markup);
        Assert.Contains("14:30", picker.Markup);
        Assert.True(picker.FindAll("button").Single(b => b.TextContent.Contains("Następny")).HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingASlot_ReportsItsStart_AndMarksTheSelection()
    {
        string? chosen = null;
        var picker = Render<SlotWeekPicker>(p => p
            .Add(c => c.Slots, Slots)
            .Add(c => c.SelectedStart, FirstWeekSlot.ToString("s"))
            .Add(c => c.SelectedStartChanged, value => chosen = value));

        var slot = picker.FindAll("button").Single(b => b.TextContent.StartsWith("09:00"));
        slot.Click();

        Assert.Equal(FirstWeekSlot.ToString("s"), chosen);
        Assert.Equal("true", slot.GetAttribute("aria-pressed"));
        Assert.Contains("Śr", slot.TextContent);
    }
}
