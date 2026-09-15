using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Web.Data;

public partial class SafePathDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Two reviewers can have the same report open. Treating status_id as a concurrency
        // token makes EF append "AND status_id = <the value we read>" to the UPDATE, so the
        // second save matches zero rows and throws instead of silently overwriting the first.
        // This is an EF mapping concern only — the database schema is untouched.
        modelBuilder.Entity<Reports>()
            .Property(r => r.StatusId)
            .IsConcurrencyToken();
    }
}
