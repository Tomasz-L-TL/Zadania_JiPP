using Microsoft.EntityFrameworkCore;
using Week09.Api.Domain;

namespace Week09.Api.Data;

public sealed class Week09DbContext(DbContextOptions<Week09DbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(24).IsRequired();
        });
        builder.Entity<Note>(entity =>
        {
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Title).HasMaxLength(100).IsRequired();
            entity.Property(note => note.Body).HasMaxLength(2000).IsRequired();
            entity.HasOne(note => note.Owner).WithMany(user => user.Notes)
                .HasForeignKey(note => note.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
