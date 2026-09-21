using System;
using System.Linq;
using HotelBooking.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotelBooking.IntegrationTests
{
    // Starts the real Web API for tests, with its own in-memory database.
    public class HotelBookingApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = "ApiTests_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<HotelBookingContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<HotelBookingContext>(opt =>
                    opt.UseInMemoryDatabase(databaseName));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HotelBookingContext>();
                var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
                initializer.Initialize(db);
            }

            return host;
        }
    }
}
