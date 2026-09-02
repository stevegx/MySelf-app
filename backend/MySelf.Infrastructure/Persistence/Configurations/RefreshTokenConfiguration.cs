using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).IsRequired();

        // Looked up by hash on every /refresh call; unique because a hash collision here
        // would mean two different sessions accepting the same cookie value.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Looked up when listing/revoking a user's sessions (logout-all, later slice).
        builder.HasIndex(t => t.UserId);
    }
}
