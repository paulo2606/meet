using Meet.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Meet.Api.Data;

public class MeetDbContext(DbContextOptions<MeetDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Meeting> Meetings => Set<Meeting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.ToTable("meetings");
            entity.HasKey(meeting => meeting.Id);
            entity.Property(meeting => meeting.Code).HasMaxLength(10).IsRequired();
            entity.HasIndex(meeting => meeting.Code).IsUnique();
            entity.HasOne(meeting => meeting.CreatedBy)
                .WithMany()
                .HasForeignKey(meeting => meeting.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
