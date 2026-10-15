using Microsoft.EntityFrameworkCore;

namespace TeamPulse.Infrastructure.Persistence;

public sealed class TeamPulseDbContext(DbContextOptions<TeamPulseDbContext> options)
    : DbContext(options);
