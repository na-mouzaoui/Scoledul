using FluentAssertions;
using Timetable.Application.Services;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;
using Timetable.Testing;

namespace Timetable.Domain.Tests;

public class SlotGeneratorTests
{
    private readonly SlotGenerator _generator = new();

    private static SchoolDay Day(Weekday weekday, int start, int end, params (string Label, int Start, int End)[] breaks)
        => new()
        {
            Id = (int)weekday,
            Weekday = weekday,
            IsActive = true,
            StartMinute = start,
            EndMinute = end,
            Breaks = breaks.Select(b => new BreakPeriod
            {
                DayId = (int)weekday,
                Label = b.Label,
                StartMinute = b.Start,
                EndMinute = b.End,
            }).ToList(),
        };

    [Fact]
    public void Reference_dataset_A_produces_six_slots_per_day()
    {
        var snapshot = ReferenceDatasets.BuildReferenceA();
        var result = _generator.Generate(snapshot.Days, 60);

        result.Slots.Should().HaveCount(30);
        result.Slots.GroupBy(s => s.DayId).Should().OnlyContain(g => g.Count() == 6);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void A_break_cuts_the_grid_and_marks_follows_break()
    {
        var day = Day(Weekday.Monday, 8 * 60, 12 * 60, ("Récréation", 10 * 60, (10 * 60) + 30));

        var result = _generator.Generate(new[] { day }, 60);

        // 08:00-09:00, 09:00-10:00, (pause 10:00-10:30), 10:30-11:30 puis 30 min résiduelles ignorées.
        result.Slots.Should().HaveCount(3);
        result.Slots.Select(s => s.SlotIndex).Should().Equal(0, 1, 2);
        result.Slots[0].StartMinute.Should().Be(8 * 60);
        result.Slots[2].StartMinute.Should().Be(10 * 60 + 30);
        result.Slots[2].FollowsBreak.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.ResidualMinutes == 30);
    }

    [Fact]
    public void Residual_time_shorter_than_a_slot_is_ignored_with_a_warning()
    {
        var day = Day(Weekday.Friday, 8 * 60, 8 * 60 + 100);

        var result = _generator.Generate(new[] { day }, 60);

        result.Slots.Should().HaveCount(1);
        result.Warnings.Should().ContainSingle(w => w.Weekday == Weekday.Friday && w.ResidualMinutes == 40);
    }

    [Fact]
    public void Inactive_days_are_ignored()
    {
        var active = Day(Weekday.Monday, 8 * 60, 10 * 60);
        var inactive = Day(Weekday.Tuesday, 8 * 60, 10 * 60);
        inactive.IsActive = false;

        var result = _generator.Generate(new[] { active, inactive }, 60);

        result.Slots.Should().HaveCount(2);
        result.Slots.Should().OnlyContain(s => s.DayId == (int)Weekday.Monday);
    }

    [Fact]
    public void Different_day_hours_produce_different_slot_counts()
    {
        var monday = Day(Weekday.Monday, 8 * 60, 16 * 60);
        var wednesday = Day(Weekday.Wednesday, 8 * 60, 12 * 60);

        var result = _generator.Generate(new[] { monday, wednesday }, 60);

        result.Slots.Count(s => s.DayId == (int)Weekday.Monday).Should().Be(8);
        result.Slots.Count(s => s.DayId == (int)Weekday.Wednesday).Should().Be(4);
    }

    [Fact]
    public void Adjacency_requires_consecutive_index_and_no_break()
    {
        var day = Day(Weekday.Monday, 8 * 60, 12 * 60, ("Pause", 9 * 60, 9 * 60 + 30));
        var slots = _generator.Generate(new[] { day }, 60).Slots;

        // créneaux : 08:00-09:00, 09:30-10:30, 10:30-11:30 (30 min résiduelles ignorées)
        slots.Should().HaveCount(3);
        SlotLayout.AreAdjacent(slots[0], slots[1]).Should().BeFalse(); // pause intercalée
        SlotLayout.AreAdjacent(slots[1], slots[2]).Should().BeTrue();
    }

    [Fact]
    public void Block_start_requires_enough_adjacent_slots()
    {
        var day = Day(Weekday.Monday, 8 * 60, 10 * 60, ("Pause", 9 * 60, 9 * 60 + 30));
        var slots = _generator.Generate(new[] { day }, 60).Slots;
        var index = SlotLayout.BuildIndex(slots);

        // 08-09 et 09:30-10:00 ne sont pas adjacents -> impossible de démarrer un bloc de 2.
        SlotLayout.CanStartBlock(slots[0], 2, index).Should().BeFalse();
    }
}
