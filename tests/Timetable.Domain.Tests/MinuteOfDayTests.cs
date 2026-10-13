using FluentAssertions;
using Timetable.Domain.Common;

namespace Timetable.Domain.Tests;

public class MinuteOfDayTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(480, "08:00")]
    [InlineData(615, "10:15")]
    [InlineData(1440, "24:00")]
    public void Format_produces_24h_representation(int minute, string expected)
        => MinuteOfDay.Format(minute).Should().Be(expected);

    [Theory]
    [InlineData("08:00", 480)]
    [InlineData("8:00", 480)]
    [InlineData("16:30", 990)]
    public void TryParse_accepts_valid_times(string text, int expected)
    {
        MinuteOfDay.TryParse(text, out var minute).Should().BeTrue();
        minute.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData("08:70")]
    [InlineData("8h00")]
    public void TryParse_rejects_invalid_times(string text)
        => MinuteOfDay.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void RoundToStep_rounds_to_five_minutes()
    {
        MinuteOfDay.RoundToStep(483).Should().Be(485);
        MinuteOfDay.RoundToStep(481).Should().Be(480);
    }
}
