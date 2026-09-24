using System;
using System.Threading.Tasks;
using HotelBooking.Core;
using HotelBooking.Infrastructure;
using HotelBooking.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBooking.IntegrationTests
{
    public class BookingManagerTests : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly IRepository<Booking> bookingRepository;
        private readonly BookingManager bookingManager;

        public BookingManagerTests()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<HotelBookingContext>()
                            .UseSqlite(connection).Options;
            var dbContext = new HotelBookingContext(options);
            IDbInitializer dbInitializer = new DbInitializer();
            dbInitializer.Initialize(dbContext);

            bookingRepository = new BookingRepository(dbContext);
            var roomRepository = new RoomRepository(dbContext);
            bookingManager = new BookingManager(bookingRepository, roomRepository);
        }

        public void Dispose()
        {
            connection.Close();
        }

        private static DateTime Days(int offset) => DateTime.Today.AddDays(offset);

        [Theory]
        [InlineData(1, 1)]    // before the booked period
        [InlineData(1, 3)]    // ends the day before the booked period starts
        [InlineData(19, 20)]  // starts the day after the booked period ends
        public async Task FindAvailableRoom_DatesDoNotOverlapSeededBookings_ReturnsARoom(int startOffset, int endOffset)
        {

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset));

            // Assert
            Assert.NotEqual(-1, roomId);
        }

        [Theory]
        [InlineData(8, 8)]    // inside the booked period
        [InlineData(4, 18)]   // exactly the booked period
        [InlineData(3, 4)]    // overlaps the first booked day
        [InlineData(18, 19)]  // overlaps the last booked day
        public async Task FindAvailableRoom_DatesOverlapSeededBookings_ReturnsMinusOne(int startOffset, int endOffset)
        {

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset));

            // Assert
            Assert.Equal(-1, roomId);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_BookingIsStoredInDatabase()
        {
            // Arrange
            var booking = new Booking { StartDate = Days(1), EndDate = Days(2), CustomerId = 1 };

            // Act
            bool created = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.True(created);
            var storedBookings = await bookingRepository.GetAllAsync();
            Assert.Contains(storedBookings, b => b.StartDate == Days(1) && b.EndDate == Days(2) && b.IsActive);
        }

        [Theory]
        [InlineData(4, 18, 15)]  // the whole booked period: day 4 to day 18
        [InlineData(1, 3, 0)]    // before the booked period
        [InlineData(19, 25, 0)]  // after the booked period
        [InlineData(2, 6, 3)]    // only days 4, 5, 6 are booked
        public async Task GetFullyOccupiedDates_SeededBookings_ReturnsExpectedNumberOfDates(
            int startOffset, int endOffset, int expectedCount)
        {

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(startOffset), Days(endOffset));

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }
    }
}
