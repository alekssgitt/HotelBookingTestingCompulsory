using System;
using System.Linq;
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
        // Real repositories + Sqlite in-memory database, seeded by DbInitializer:
        // 3 rooms, all booked from day 4 through day 18.
        SqliteConnection connection;
        IRepository<Booking> bookingRepository;
        BookingManager bookingManager;

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
            var roomRepos = new RoomRepository(dbContext);
            bookingManager = new BookingManager(bookingRepository, roomRepos);
        }

        public void Dispose()
        {
            connection.Close();
        }

        private static DateTime Days(int offset) => DateTime.Today.AddDays(offset);

        [Fact]
        public async Task FindAvailableRoom_RoomNotAvailable_RoomIdIsMinusOne()
        {
            var roomId =
                await bookingManager.FindAvailableRoom(DateTime.Today.AddDays(8), DateTime.Today.AddDays(8));

            Assert.Equal(-1, roomId);
        }

        [Theory]
        [InlineData(0, 1)]   // start is today
        [InlineData(-2, 3)]  // start is in the past
        [InlineData(5, 4)]   // start is after end
        public async Task FindAvailableRoom_InvalidDates_ThrowsArgumentException(int startOffset, int endOffset)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset)));
        }

        [Theory]
        [InlineData(1, 1, true)]     // tomorrow: before anything is booked
        [InlineData(2, 3, true)]     // the two days right before the occupied period
        [InlineData(19, 20, true)]   // the two days right after the occupied period
        [InlineData(8, 8, false)]    // inside the occupied period
        [InlineData(4, 18, false)]   // the whole occupied period
        [InlineData(3, 4, false)]    // overlaps the first occupied day
        [InlineData(18, 19, false)]  // overlaps the last occupied day
        [InlineData(1, 5, false)]    // starts free, ends inside the occupied period
        [InlineData(16, 22, false)]  // starts inside, ends after
        public async Task FindAvailableRoom_SeededBookings_ReturnsRoomOnlyWhenDatesAreFree(
            int startOffset, int endOffset, bool roomShouldBeAvailable)
        {
            int roomId = await bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset));

            if (roomShouldBeAvailable)
            {
                Assert.NotEqual(-1, roomId);

                var overlapping = (await bookingRepository.GetAllAsync()).Where(b =>
                    b.RoomId == roomId &&
                    b.IsActive &&
                    b.StartDate <= Days(endOffset) &&
                    b.EndDate >= Days(startOffset));
                Assert.Empty(overlapping);
            }
            else
            {
                Assert.Equal(-1, roomId);
            }
        }

        [Theory]
        [InlineData(4, 18, 15)] // the full occupied period: 4,5,...,18
        [InlineData(4, 4, 1)]   // a single occupied day
        [InlineData(1, 3, 0)]   // before anything is booked
        [InlineData(19, 25, 0)] // after everything is booked
        [InlineData(2, 6, 3)]   // only 4, 5, 6 fall in the occupied period
        [InlineData(16, 20, 3)] // only 16, 17, 18 fall in the occupied period
        public async Task GetFullyOccupiedDates_SeededBookings_ReturnsExpectedNumberOfDates(
            int startOffset, int endOffset, int expectedCount)
        {
            var result = await bookingManager.GetFullyOccupiedDates(Days(startOffset), Days(endOffset));

            Assert.Equal(expectedCount, result.Count);
        }
    }
}
