using Microsoft.EntityFrameworkCore;
using Shared.Account;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class MainDataContext : DbContext
    {
        public MainDataContext(DbContextOptions<MainDataContext> options):base(options)
        {
            
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Role> Roles { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Account>()
                .HasMany(a => a.Roles)
                .WithMany(r => r.Accounts)
                .UsingEntity<AccountRole>(
                    j => j
                        .HasOne(ar => ar.Role)
                        .WithMany(r => r.AccountRoles)
                        .HasForeignKey(r => r.RoleId),
                    j => j
                        .HasOne(ar => ar.Account)
                        .WithMany(a => a.AccountRoles)
                        .HasForeignKey(ar => ar.AccountId),
                    j =>
                    {
                        //would do it this way if using database times, but application times are currently used
                        //j.Property(ar => ar.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                        j.HasKey(t => new { t.AccountId, t.RoleId });
                    }
                );
        }
    }
}
