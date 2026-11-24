/// The calendar fields of a moment in local time, worked out with integer arithmetic only, so the
/// crash handler can stamp `errors.log` without calling anything that allocates or takes a lock.
public struct LogTimestamp: Equatable, Sendable {
    public var year: Int
    public var month: Int
    public var day: Int
    public var hour: Int
    public var minute: Int
    public var second: Int

    public init(epochSeconds: Int, utcOffsetSeconds: Int) {
        let local = epochSeconds + utcOffsetSeconds
        var days = local / 86_400
        var rest = local % 86_400
        if rest < 0 {
            rest += 86_400
            days -= 1
        }
        hour = rest / 3600
        minute = rest % 3600 / 60
        second = rest % 60

        // Days since 1970-01-01 to a proleptic Gregorian date (Howard Hinnant's civil_from_days).
        let shifted = days + 719_468
        let era = (shifted >= 0 ? shifted : shifted - 146_096) / 146_097
        let dayOfEra = shifted - era * 146_097
        let yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36_524 - dayOfEra / 146_096) / 365
        let dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100)
        let monthIndex = (5 * dayOfYear + 2) / 153
        day = dayOfYear - (153 * monthIndex + 2) / 5 + 1
        month = monthIndex < 10 ? monthIndex + 3 : monthIndex - 9
        year = yearOfEra + era * 400 + (month <= 2 ? 1 : 0)
    }
}
