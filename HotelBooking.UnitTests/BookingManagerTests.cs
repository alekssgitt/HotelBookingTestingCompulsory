using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using HotelBooking.Core;
using Xunit;

namespace HotelBooking.UnitTests
{
    public class BookingManagerTests
    {
        private readonly IRepository<Booking> bookingRepository;
        private readonly IRepository<Room> roomRepository;
        private readonly List<Booking> bookings;
        private readonly BookingManager bookingManager;

        public BookingManagerTests()
        {
            bookings = new List<Booking>();
            var rooms = new List<Room>
            {
                new Room { Id = 1, Description = "Room 1" },
                new Room { Id = 2, Description = "Room 2" }
            };

            bookingRepository = A.Fake<IRepository<Booking>>();
            roomRepository = A.Fake<IRepository<Room>>();
            A.CallTo(() => bookingRepository.GetAllAsync()).Returns(bookings);
            A.CallTo(() => roomRepository.GetAllAsync()).Returns(rooms);

            bookingManager = new BookingManager(bookingRepository, roomRepository);
        }

        private static DateTime Days(int offset) => DateTime.Today.AddDays(offset);

        private void AddBooking(int roomId, int startOffset, int endOffset, bool isActive = true)
        {
            bookings.Add(new Booking
            {
                Id = bookings.Count + 1,
                RoomId = roomId,
                StartDate = Days(startOffset),
                EndDate = Days(endOffset),
                IsActive = isActive,
                CustomerId = 1
            });
        }

        private void OccupyBothRoomsFromDay10To20()
        {
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20);
            AddBooking(roomId: 2, startOffset: 10, endOffset: 20);
        }

        // FindAvailibleRoom method tests

        [Theory]
        [InlineData(0, 1)]   // start is today
        [InlineData(-2, 3)]  // start is in the past
        [InlineData(5, 4)]   // start is after end
        public async Task FindAvailableRoom_InvalidDates_ThrowsArgumentException(int startOffset, int endOffset)
        {
            // Arrange
            DateTime startDate = Days(startOffset);
            DateTime endDate = Days(endOffset);

            // Act
            Task act() => bookingManager.FindAvailableRoom(startDate, endDate);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Theory]
        [InlineData(1, 1)]    // tomorrow, long before the booked period
        [InlineData(8, 9)]    // ends the day before the booked period starts
        [InlineData(21, 22)]  // starts the day after the booked period ends
        public async Task FindAvailableRoom_DatesDoNotOverlapBookings_ReturnsARoom(int startOffset, int endOffset)
        {
            // Arrange
            OccupyBothRoomsFromDay10To20();

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset));

            // Assert
            Assert.NotEqual(-1, roomId);
        }

        [Theory]
        [InlineData(10, 20)]  // exactly the booked period
        [InlineData(15, 15)]  // one day in the middle
        [InlineData(9, 10)]   // overlaps the first booked day
        [InlineData(20, 21)]  // overlaps the last booked day
        [InlineData(5, 12)]   // starts before, ends inside
        [InlineData(18, 25)]  // starts inside, ends after
        [InlineData(5, 25)]   // surrounds the whole booked period
        public async Task FindAvailableRoom_DatesOverlapBookingsInAllRooms_ReturnsMinusOne(int startOffset, int endOffset)
        {
            // Arrange
            OccupyBothRoomsFromDay10To20();

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(startOffset), Days(endOffset));

            // Assert
            Assert.Equal(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_OnlyRoom1Booked_ReturnsRoom2()
        {
            // Arrange
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20);

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(10), Days(20));

            // Assert
            Assert.Equal(2, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_Room1BookingIsInactive_ReturnsRoom1()
        {
            // Arrange
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20, isActive: false);
            AddBooking(roomId: 2, startOffset: 10, endOffset: 20);

            // Act
            int roomId = await bookingManager.FindAvailableRoom(Days(10), Days(20));

            // Assert
            Assert.Equal(1, roomId);
        }

        // CreateBooking method tests

        [Fact]
        public async Task CreateBooking_RoomAvailable_ReturnsTrueAndSavesActiveBooking()
        {
            // Arrange
            var booking = new Booking { StartDate = Days(1), EndDate = Days(3), CustomerId = 1 };

            // Act
            bool created = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.True(created);
            Assert.True(booking.IsActive);
            Assert.Equal(1, booking.RoomId);
            A.CallTo(() => bookingRepository.AddAsync(booking)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            OccupyBothRoomsFromDay10To20();
            var booking = new Booking { StartDate = Days(10), EndDate = Days(20), CustomerId = 1 };

            // Act
            bool created = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.False(created);
            A.CallTo(() => bookingRepository.AddAsync(A<Booking>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task CreateBooking_StartDateIsToday_ThrowsAndDoesNotSave()
        {
            // Arrange
            var booking = new Booking { StartDate = DateTime.Today, EndDate = Days(2), CustomerId = 1 };

            // Act
            Task act() => bookingManager.CreateBooking(booking);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(act);
            A.CallTo(() => bookingRepository.AddAsync(A<Booking>._)).MustNotHaveHappened();
        }

        // GetFullyOccupiedDates method tests

        [Fact]
        public async Task GetFullyOccupiedDates_StartAfterEnd_ThrowsArgumentException()
        {
            // Arrange
            DateTime startDate = Days(5);
            DateTime endDate = Days(4);

            // Act
            Task act() => bookingManager.GetFullyOccupiedDates(startDate, endDate);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_NoBookings_ReturnsEmptyList()
        {
            // Arrange
            // Nothing to add: the shared setup has no bookings.

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(1), Days(5));

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_OnlyOneOfTwoRoomsBooked_ReturnsEmptyList()
        {
            // Arrange
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(10), Days(20));

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_BookingsAreInactive_ReturnsEmptyList()
        {
            // Arrange
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20, isActive: false);
            AddBooking(roomId: 2, startOffset: 10, endOffset: 20, isActive: false);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(10), Days(20));

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(10, 20, 11)] // the whole booked period: day 10 to day 20
        [InlineData(10, 10, 1)]  // a single booked day
        [InlineData(1, 5, 0)]    // before the booked period
        [InlineData(21, 25, 0)]  // after the booked period
        [InlineData(8, 12, 3)]   // only days 10, 11, 12 are booked
        [InlineData(18, 22, 3)]  // only days 18, 19, 20 are booked
        public async Task GetFullyOccupiedDates_BothRoomsBookedDay10To20_ReturnsExpectedNumberOfDates(
            int startOffset, int endOffset, int expectedCount)
        {
            // Arrange
            OccupyBothRoomsFromDay10To20();

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(startOffset), Days(endOffset));

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_PeriodSurroundsBookedDays_ReturnsFirstAndLastBookedDay()
        {
            // Arrange
            OccupyBothRoomsFromDay10To20();

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(Days(8), Days(22));

            // Assert
            Assert.Equal(Days(10), result.First());
            Assert.Equal(Days(20), result.Last());
        }
    }
}
