using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HotelBooking.Core;
using Xunit;

namespace HotelBooking.IntegrationTests
{
    // Full cycle: HTTP → controller → BookingManager/repository → database.
    public class HotelBookingApiTests : IClassFixture<HotelBookingApiFactory>
    {
        private readonly HttpClient client;

        public HotelBookingApiTests(HotelBookingApiFactory factory)
        {
            client = factory.CreateClient();
        }

        [Fact]
        public async Task GetRooms_ReturnsTheThreeSeededRooms()
        {
            var response = await client.GetAsync("/rooms");
            var rooms = await response.Content.ReadFromJsonAsync<List<Room>>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, rooms.Count);
        }

        [Fact]
        public async Task GetBooking_UnknownId_ReturnsNotFound()
        {
            var response = await client.GetAsync("/bookings/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PostBooking_FullyOccupiedDates_ReturnsConflict()
        {
            var booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(8),
                EndDate = DateTime.Today.AddDays(8),
                CustomerId = 1
            };

            var response = await client.PostAsJsonAsync("/bookings", booking);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task PostBooking_FreeDates_CreatesBookingThatCanBeReadBack()
        {
            var start = DateTime.Today.AddDays(1);
            var end = DateTime.Today.AddDays(2);
            var booking = new Booking
            {
                StartDate = start,
                EndDate = end,
                CustomerId = 1
            };

            var postResponse = await client.PostAsJsonAsync("/bookings", booking);
            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

            var bookings = await client.GetFromJsonAsync<List<Booking>>("/bookings");
            Assert.Contains(bookings, b =>
                b.CustomerId == 1 &&
                b.IsActive &&
                b.StartDate.Date == start.Date &&
                b.EndDate.Date == end.Date);
        }
    }
}
