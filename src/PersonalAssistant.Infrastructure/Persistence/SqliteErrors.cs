using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PersonalAssistant.Infrastructure.Persistence;

internal static class SqliteErrors
{
    private const int ConstraintViolationCode = 19;

    /// <summary>
    /// True when the save failed because a unique index already has that value.
    /// </summary>
    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: ConstraintViolationCode };
}
