namespace FreeTrain.Online.Simulation;

public enum Season { Spring, Summer, Autumn, Winter }
public enum DayNight { Day, Night }

// ExAV 2.1.1.12, 883cf7d/trunk/core/world/{Time,TimeLength}.cs.
// Original copyright Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
// This is a fixed 365-day calendar, independent of DateTime, UTC and leap years.
public readonly record struct GameTime
{
    public const long Hour = 60, Day = 1440, Year = 525600;
    public const long StartMinute = 130080; // Year 1, April 1, 08:00.
    // Bounded portable representation; year and all arithmetic stay representable.
    public const long MaximumMinute = (long)int.MaxValue * Year - 1;
    private static readonly int[] MonthDays = [31,28,31,30,31,30,31,31,30,31,30,31];
    public long AbsoluteMinute { get; }
    public GameTime(long absoluteMinute)
    {
        if (absoluteMinute < 0 || absoluteMinute > MaximumMinute) throw new ArgumentOutOfRangeException(nameof(absoluteMinute));
        AbsoluteMinute = absoluteMinute;
    }
    public long ElapsedMinutes => AbsoluteMinute - StartMinute;
    public int YearNumber => (int)(AbsoluteMinute / Year) + 1;
    public int DayOfWeek => (int)(AbsoluteMinute / Day % 7); // Sunday = 0.
    public int HourOfDay => (int)(AbsoluteMinute / Hour % 24);
    public int Minute => (int)(AbsoluteMinute % Hour);
    private (int Month, int Day) Date
    {
        get
        {
            var days = (int)(AbsoluteMinute / Day % 365);
            for (var i = 0; i < MonthDays.Length; i++)
            { if (days < MonthDays[i]) return (i + 1, days + 1); days -= MonthDays[i]; }
            throw new InvalidOperationException();
        }
    }
    public int Month => Date.Month;
    public int DayOfMonth => Date.Day;
    public Season Season => (Season)((Month + 9) % 12 / 3);
    public DayNight DayOrNight => HourOfDay is >= 6 and < 18 ? DayNight.Day : DayNight.Night;
    public bool IsWeekend => DayOfWeek is 0 or 6;
    public bool IsHoliday => (Month, DayOfMonth) is (1,1 or 2 or 3 or 15) or (2,11) or (3,21) or (4,29)
        or (5,3 or 4 or 5) or (7,20) or (9,15 or 23) or (10,10) or (11,3 or 23) or (12,23 or 30 or 31);
    public bool IsVacation => (Month, DayOfMonth) is (1, < 6) or (3, > 24) or (4, < 6)
        or (7, > 24) or (8, _) or (12, > 24);
    public long MinutesUntilTomorrow => Day - AbsoluteMinute % Day; // Midnight returns 1440, not zero.
    public GameTime AddMinutes(long minutes) => new(checked(AbsoluteMinute + minutes));
}
