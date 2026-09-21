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
        // The hotel used in every test: two rooms.
        // Each test starts with an empty booking list and adds only what it needs.
        private readonly IRepository<Booking> bookingRepository;
        private readonly IRepository<Room> roomRepository;
        private readonly List<Booking> bookings;
        private readonly BookingManager bookingManager;

        public BookingManagerTests()
        {
            bookings = new List<Booking>();
            var rooms = new List<Room>
            {
                new Room { Id = 1, Description = "Single" },
                new Room { Id = 2, Description = "Double" }
            };

            bookingRepository = A.Fake<IRepository<Booking>>();
            roomRepository = A.Fake<IRepository<Room>>();

            A.CallTo(() => bookingRepository.GetAllAsync()).Returns(bookings);
            A.CallTo(() => roomRepository.GetAllAsync()).Returns(rooms);

            bookingManager = new BookingManager(bookingRepository, roomRepository);
        }

        // Days(1) = tomorrow, Days(10) = 10 days from today, and so on.
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

        [Fact]
        public async Task FindAvailableRoom_StartDateNotInTheFuture_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => bookingManager.FindAvailableRoom(DateTime.Today, Days(1)));
        }

        [Fact]
        public async Task FindAvailableRoom_StartAfterEnd_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => bookingManager.FindAvailableRoom(Days(5), Days(4)));
        }

        [Fact]
        public async Task FindAvailableRoom_DatesAreFree_ReturnsAFreeRoom()
        {
            OccupyBothRoomsFromDay10To20();

            int roomId = await bookingManager.FindAvailableRoom(Days(1), Days(1));

            Assert.NotEqual(-1, roomId);

            var overlapping = bookings.Where(b =>
                b.RoomId == roomId &&
                b.IsActive &&
                b.StartDate <= Days(1) &&
                b.EndDate >= Days(1));
            Assert.Empty(overlapping);
        }

        [Fact]
        public async Task FindAvailableRoom_AllRoomsOccupied_ReturnsMinusOne()
        {
            OccupyBothRoomsFromDay10To20();

            int roomId = await bookingManager.FindAvailableRoom(Days(10), Days(20));

            Assert.Equal(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_OnlyRoom1Occupied_ReturnsRoom2()
        {
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20);

            int roomId = await bookingManager.FindAvailableRoom(Days(10), Days(20));

            Assert.Equal(2, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_InactiveBookingDoesNotBlockRoom_ReturnsThatRoom()
        {
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20, isActive: false);
            AddBooking(roomId: 2, startOffset: 10, endOffset: 20, isActive: true);

            int roomId = await bookingManager.FindAvailableRoom(Days(10), Days(20));

            Assert.Equal(1, roomId);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_SavesActiveBookingAndReturnsTrue()
        {
            var booking = new Booking
            {
                StartDate = Days(1),
                EndDate = Days(3),
                CustomerId = 1
            };

            bool created = await bookingManager.CreateBooking(booking);

            Assert.True(created);
            Assert.True(booking.IsActive);
            Assert.Equal(1, booking.RoomId);
            A.CallTo(() => bookingRepository.AddAsync(booking)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_DoesNotSaveAndReturnsFalse()
        {
            OccupyBothRoomsFromDay10To20();
            var booking = new Booking
            {
                StartDate = Days(10),
                EndDate = Days(20),
                CustomerId = 1
            };

            bool created = await bookingManager.CreateBooking(booking);

            Assert.False(created);
            A.CallTo(() => bookingRepository.AddAsync(A<Booking>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task CreateBooking_InvalidDates_ThrowsArgumentException()
        {
            var booking = new Booking
            {
                StartDate = DateTime.Today,
                EndDate = Days(2)
            };

            await Assert.ThrowsAsync<ArgumentException>(
                () => bookingManager.CreateBooking(booking));
            A.CallTo(() => bookingRepository.AddAsync(A<Booking>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task GetFullyOccupiedDates_StartAfterEnd_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => bookingManager.GetFullyOccupiedDates(Days(5), Days(4)));
        }

        [Fact]
        public async Task GetFullyOccupiedDates_NoBookings_ReturnsEmptyList()
        {
            var result = await bookingManager.GetFullyOccupiedDates(Days(1), Days(5));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_OnlyOneRoomBooked_ReturnsEmptyList()
        {
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20);

            var result = await bookingManager.GetFullyOccupiedDates(Days(10), Days(20));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_InactiveBookingsDoNotOccupyHotel_ReturnsEmptyList()
        {
            AddBooking(roomId: 1, startOffset: 10, endOffset: 20, isActive: false);
            AddBooking(roomId: 2, startOffset: 10, endOffset: 20, isActive: false);

            var result = await bookingManager.GetFullyOccupiedDates(Days(10), Days(20));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_RequestWiderThanOccupiedPeriod_ReturnsExactlyTheOccupiedDays()
        {
            OccupyBothRoomsFromDay10To20();

            var result = await bookingManager.GetFullyOccupiedDates(Days(8), Days(22));

            Assert.Equal(11, result.Count);
            Assert.Equal(Days(10), result.First());
            Assert.Equal(Days(20), result.Last());
        }
    }
}
