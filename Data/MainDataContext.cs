using Microsoft.EntityFrameworkCore;
using Shared;
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

        public DbSet<WeatherForecast> Forecasts { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
