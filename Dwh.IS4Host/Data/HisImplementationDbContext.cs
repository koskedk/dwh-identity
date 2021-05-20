using Dwh.IS4Host.Models;
using Microsoft.EntityFrameworkCore;

namespace Dwh.IS4Host.Data
{
    public class HisImplementationDbContext : DbContext
    {
        public HisImplementationDbContext(DbContextOptions<HisImplementationDbContext> options) : base(options)
        {

        }

        public DbSet<UsgPartnerMenchanism> UsgPartnerMenchanisms { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<UsgPartnerMenchanism>(builder =>
                {
                    builder.HasNoKey();
                    builder.ToTable("lkp_USGPartnerMenchanism");
                });
        }
    }
}